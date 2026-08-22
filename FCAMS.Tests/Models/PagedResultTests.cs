using FCAMS.Web.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FCAMS.Tests.Models
{
    [TestClass]
    public class PagedResultTests
    {
        [TestMethod]
        public void TotalPages_ExactMultiple_ComputesCorrectly()
        {
            var result = new PagedResult<string> { TotalRowCount = 100, PageSize = 25 };
            Assert.AreEqual(4, result.TotalPages);
        }

        [TestMethod]
        public void TotalPages_PartialLastPage_RoundsUp()
        {
            var result = new PagedResult<string> { TotalRowCount = 101, PageSize = 25 };
            Assert.AreEqual(5, result.TotalPages);
        }

        [TestMethod]
        public void TotalPages_ZeroRows_ReturnsZero()
        {
            var result = new PagedResult<string> { TotalRowCount = 0, PageSize = 25 };
            Assert.AreEqual(0, result.TotalPages);
        }

        [TestMethod]
        public void TotalPages_ZeroPageSize_DoesNotDivideByZero()
        {
            var result = new PagedResult<string> { TotalRowCount = 50, PageSize = 0 };
            Assert.AreEqual(0, result.TotalPages);
        }
    }
}
