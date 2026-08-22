using System;
using FCAMS.Web.DAL.Repositories;
using FCAMS.Web.Models;
using FCAMS.Web.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace FCAMS.Tests.Services
{
    [TestClass]
    public class AuthenticationServiceTests
    {
        private Mock<IUserRepository> _userRepo;
        private AuthenticationService _service;

        [TestInitialize]
        public void Setup()
        {
            _userRepo = new Mock<IUserRepository>();
            _service = new AuthenticationService(_userRepo.Object);
        }

        private static User MakeUser(string password, bool isActive = true, DateTime? lockedOutUntil = null)
        {
            var salt = PasswordHasher.GenerateSalt();
            return new User
            {
                UserId = 1,
                Username = "jdoe",
                PasswordSalt = salt,
                PasswordHash = PasswordHasher.ComputeHash(password, salt),
                IsActive = isActive,
                LockedOutUntil = lockedOutUntil,
                RoleName = "Auditor"
            };
        }

        [TestMethod]
        public void Authenticate_CorrectCredentials_ReturnsSuccess()
        {
            var user = MakeUser("Secret123!");
            _userRepo.Setup(r => r.GetForAuthentication("jdoe")).Returns(user);

            var outcome = _service.Authenticate("jdoe", "Secret123!");

            Assert.AreEqual(LoginResult.Success, outcome.Result);
            _userRepo.Verify(r => r.RecordLoginSuccess(user.UserId), Times.Once);
        }

        [TestMethod]
        public void Authenticate_WrongPassword_ReturnsInvalidCredentialsAndRecordsFailure()
        {
            var user = MakeUser("Secret123!");
            _userRepo.Setup(r => r.GetForAuthentication("jdoe")).Returns(user);

            var outcome = _service.Authenticate("jdoe", "WrongPassword");

            Assert.AreEqual(LoginResult.InvalidCredentials, outcome.Result);
            _userRepo.Verify(r => r.RecordLoginFailure(user.UserId), Times.Once);
        }

        [TestMethod]
        public void Authenticate_UnknownUsername_ReturnsInvalidCredentialsWithoutThrowing()
        {
            _userRepo.Setup(r => r.GetForAuthentication("ghost")).Returns((User)null);

            var outcome = _service.Authenticate("ghost", "anything");

            Assert.AreEqual(LoginResult.InvalidCredentials, outcome.Result);
        }

        [TestMethod]
        public void Authenticate_InactiveAccount_ReturnsAccountInactive()
        {
            var user = MakeUser("Secret123!", isActive: false);
            _userRepo.Setup(r => r.GetForAuthentication("jdoe")).Returns(user);

            var outcome = _service.Authenticate("jdoe", "Secret123!");

            Assert.AreEqual(LoginResult.AccountInactive, outcome.Result);
        }

        [TestMethod]
        public void Authenticate_LockedOutAccount_ReturnsAccountLockedOut()
        {
            var user = MakeUser("Secret123!", lockedOutUntil: DateTime.UtcNow.AddMinutes(10));
            _userRepo.Setup(r => r.GetForAuthentication("jdoe")).Returns(user);

            var outcome = _service.Authenticate("jdoe", "Secret123!");

            Assert.AreEqual(LoginResult.AccountLockedOut, outcome.Result);
        }

        [TestMethod]
        public void Authenticate_LockoutExpired_AllowsLoginAttempt()
        {
            var user = MakeUser("Secret123!", lockedOutUntil: DateTime.UtcNow.AddMinutes(-1));
            _userRepo.Setup(r => r.GetForAuthentication("jdoe")).Returns(user);

            var outcome = _service.Authenticate("jdoe", "Secret123!");

            Assert.AreEqual(LoginResult.Success, outcome.Result);
        }

        [TestMethod]
        public void Authenticate_EmptyUsername_ReturnsInvalidCredentialsWithoutQueryingRepository()
        {
            var outcome = _service.Authenticate("", "Secret123!");

            Assert.AreEqual(LoginResult.InvalidCredentials, outcome.Result);
            _userRepo.Verify(r => r.GetForAuthentication(It.IsAny<string>()), Times.Never);
        }
    }
}
