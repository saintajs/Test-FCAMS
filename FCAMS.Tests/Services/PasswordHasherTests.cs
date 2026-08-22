using FCAMS.Web.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FCAMS.Tests.Services
{
    [TestClass]
    public class PasswordHasherTests
    {
        [TestMethod]
        public void VerifyPassword_CorrectPassword_ReturnsTrue()
        {
            var salt = PasswordHasher.GenerateSalt();
            var hash = PasswordHasher.ComputeHash("Correct-Horse-1", salt);

            bool result = PasswordHasher.VerifyPassword("Correct-Horse-1", salt, hash);

            Assert.IsTrue(result);
        }

        [TestMethod]
        public void VerifyPassword_WrongPassword_ReturnsFalse()
        {
            var salt = PasswordHasher.GenerateSalt();
            var hash = PasswordHasher.ComputeHash("Correct-Horse-1", salt);

            bool result = PasswordHasher.VerifyPassword("Wrong-Password", salt, hash);

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void ComputeHash_SamePasswordDifferentSalt_ProducesDifferentHashes()
        {
            var salt1 = PasswordHasher.GenerateSalt();
            var salt2 = PasswordHasher.GenerateSalt();

            var hash1 = PasswordHasher.ComputeHash("SamePassword123", salt1);
            var hash2 = PasswordHasher.ComputeHash("SamePassword123", salt2);

            CollectionAssert.AreNotEqual(hash1, hash2);
        }

        [TestMethod]
        public void GenerateSalt_TwoCalls_ProducesDifferentSalts()
        {
            var salt1 = PasswordHasher.GenerateSalt();
            var salt2 = PasswordHasher.GenerateSalt();

            CollectionAssert.AreNotEqual(salt1, salt2);
        }

        [TestMethod]
        public void ComputeHash_SamePasswordAndSalt_IsDeterministic()
        {
            var salt = PasswordHasher.GenerateSalt();

            var hash1 = PasswordHasher.ComputeHash("Determinism123", salt);
            var hash2 = PasswordHasher.ComputeHash("Determinism123", salt);

            CollectionAssert.AreEqual(hash1, hash2);
        }
    }
}
