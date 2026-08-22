using System;
using FCAMS.Web.DAL.Repositories;
using FCAMS.Web.Models;

namespace FCAMS.Web.Services
{
    public enum LoginResult
    {
        Success,
        InvalidCredentials,
        AccountLockedOut,
        AccountInactive
    }

    public class LoginOutcome
    {
        public LoginResult Result { get; set; }
        public User User { get; set; }
    }

    public interface IAuthenticationService
    {
        LoginOutcome Authenticate(string username, string password);
    }

    /// <summary>
    /// Encapsulates the login business rules: account lockout after N
    /// failed attempts, inactive-account rejection, and the salted-hash
    /// comparison. Kept separate from AccountController so it is unit
    /// testable without spinning up MVC's HttpContext.
    /// </summary>
    public class AuthenticationService : IAuthenticationService
    {
        private readonly IUserRepository _userRepository;
        private const int MaxFailedAttempts = 5;

        public AuthenticationService(IUserRepository userRepository)
        {
            _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        }

        public LoginOutcome Authenticate(string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                return new LoginOutcome { Result = LoginResult.InvalidCredentials };

            var user = _userRepository.GetForAuthentication(username.Trim());
            if (user == null)
            {
                // Do not distinguish "no such user" from "wrong password" in
                // the result -- both surface identically to the caller so a
                // brute-force script can't use the response to enumerate
                // valid usernames.
                return new LoginOutcome { Result = LoginResult.InvalidCredentials };
            }

            if (!user.IsActive)
            {
                return new LoginOutcome { Result = LoginResult.AccountInactive, User = user };
            }

            if (user.LockedOutUntil.HasValue && user.LockedOutUntil.Value > DateTime.UtcNow)
            {
                return new LoginOutcome { Result = LoginResult.AccountLockedOut, User = user };
            }

            bool passwordOk = PasswordHasher.VerifyPassword(password, user.PasswordSalt, user.PasswordHash);
            if (!passwordOk)
            {
                _userRepository.RecordLoginFailure(user.UserId);
                return new LoginOutcome { Result = LoginResult.InvalidCredentials };
            }

            _userRepository.RecordLoginSuccess(user.UserId);
            return new LoginOutcome { Result = LoginResult.Success, User = user };
        }
    }
}
