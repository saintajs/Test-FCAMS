using System;
using System.Collections.Generic;
using System.Web.Mvc;
using FCAMS.Web.DAL;
using FCAMS.Web.DAL.Repositories;
using FCAMS.Web.Infrastructure;
using FCAMS.Web.Models;
using FCAMS.Web.Models.ViewModels;
using FCAMS.Web.Services;

namespace FCAMS.Web.Controllers
{
    [AuthorizeRole("Administrator")]
    public class AdminController : Controller
    {
        private readonly IUserRepository _userRepository;
        private readonly IAuditLogRepository _auditLogRepository;
        private readonly IExceptionLogRepository _exceptionLogRepository;

        public AdminController()
            : this(
                new UserRepository(),
                new AuditLogRepository(),
                new ExceptionLogRepository())
        {
        }

        public AdminController(
            IUserRepository userRepository,
            IAuditLogRepository auditLogRepository,
            IExceptionLogRepository exceptionLogRepository)
        {
            _userRepository = userRepository;
            _auditLogRepository = auditLogRepository;
            _exceptionLogRepository = exceptionLogRepository;
        }

        // GET: /Admin/Users
        public ActionResult Users()
        {
            return View(_userRepository.GetAll());
        }

        // GET: /Admin/Create
        [HttpGet]
        public ActionResult Create()
        {
            var model = new UserCreateViewModel();

            LoadLists(model);

            return View(model);
        }

        // POST: /Admin/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(UserCreateViewModel model)
        {
            if (!ModelState.IsValid)
            {
                LoadLists(model);
                return View(model);
            }

            try
            {
                var salt = PasswordHasher.GenerateSalt();
                var hash = PasswordHasher.ComputeHash(model.Password, salt);

                var user = new User
                {
                    Username = model.Username.Trim(),
                    Email = model.Email.Trim(),
                    FullName = model.FullName.Trim(),
                    PasswordHash = hash,
                    PasswordSalt = salt,
                    RoleId = model.RoleId,
                    DepartmentId = model.DepartmentId,
                    IsActive = true
                };

                _userRepository.Create(user);

                TempData["SuccessMessage"] =
                    "User '" + user.Username + "' created successfully.";

                return RedirectToAction("Users");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", ex.Message);
                LoadLists(model);
                return View(model);
            }
        }

        // GET: /Admin/EditUser/3
        [HttpGet]
        public ActionResult EditUser(int id)
        {
            var user = _userRepository.GetById(id);

            if (user == null)
                return HttpNotFound();

            var model = new UserEditViewModel
            {
                UserId = user.UserId,
                Username = user.Username,
                Email = user.Email,
                FullName = user.FullName,
                RoleId = user.RoleId,
                DepartmentId = user.DepartmentId,
                IsActive = user.IsActive
            };

            LoadLists(model);

            return View(model);
        }

        // POST: /Admin/EditUser/3
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditUser(UserEditViewModel model)
        {
            if (!ModelState.IsValid)
            {
                LoadLists(model);
                return View(model);
            }

            try
            {
                var user = new User
                {
                    UserId = model.UserId,
                    Username = model.Username.Trim(),
                    Email = model.Email.Trim(),
                    FullName = model.FullName.Trim(),
                    RoleId = model.RoleId,
                    DepartmentId = model.DepartmentId,
                    IsActive = model.IsActive
                };

                // Password is optional during editing.
                // Empty password means keep the existing password.
                if (!string.IsNullOrWhiteSpace(model.Password))
                {
                    var salt = PasswordHasher.GenerateSalt();
                    var hash = PasswordHasher.ComputeHash(
                        model.Password,
                        salt);

                    user.PasswordHash = hash;
                    user.PasswordSalt = salt;
                }

                _userRepository.Update(user);

                TempData["SuccessMessage"] =
                    "User '" + user.Username + "' updated successfully.";

                return RedirectToAction("Users");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", ex.Message);
                LoadLists(model);
                return View(model);
            }
        }

        // GET: /Admin/UserDetails/3
        [HttpGet]
        public ActionResult UserDetails(int id)
        {
            var user = _userRepository.GetById(id);

            if (user == null)
                return HttpNotFound();

            return View(user);
        }

        // GET: /Admin/AuditTrail
        public ActionResult AuditTrail(
            int? userId,
            string actionType,
            DateTime? dateFrom,
            DateTime? dateTo,
            int pageNumber = 1)
        {
            var result = _auditLogRepository.GetPaged(
                userId,
                actionType,
                dateFrom,
                dateTo,
                pageNumber,
                50);

            return View(result);
        }

        // GET: /Admin/ExceptionLog
        public ActionResult ExceptionLog(bool unresolvedOnly = true)
        {
            ViewBag.UnresolvedOnly = unresolvedOnly;

            return View(
                _exceptionLogRepository.GetRecent(
                    100,
                    unresolvedOnly));
        }

        private void LoadLists(UserCreateViewModel model)
        {
            ViewBag.Roles = GetRoles();
            model.Departments = GetDepartments();
        }

        private void LoadLists(UserEditViewModel model)
        {
            ViewBag.Roles = GetRoles();
            model.Departments = GetDepartments();
        }

        private List<Role> GetRoles()
        {
            using (var db = new SqlDataAccess())
            {
                return db.ExecuteReader(
                    "dbo.usp_Role_GetAll",
                    reader => new Role
                    {
                        RoleId = reader.GetInt32(
                            reader.GetOrdinal("RoleId")),

                        RoleName = reader.GetString(
                            reader.GetOrdinal("RoleName")),

                        Description = reader.GetStringOrNull("Description")
                    });
            }
        }

        private List<Department> GetDepartments()
        {
            using (var db = new SqlDataAccess())
            {
                return db.ExecuteReader(
                    "dbo.usp_Department_GetActive",
                    reader => new Department
                    {
                        DepartmentId = reader.GetInt32(
                            reader.GetOrdinal("DepartmentId")),

                        DepartmentName = reader.GetString(
                            reader.GetOrdinal("DepartmentName")),

                        CostCenterCode = reader.GetStringOrNull("CostCenterCode"),

                        IsActive = reader.GetBoolean(
                            reader.GetOrdinal("IsActive"))
                    });
            }
        }
    }
}

