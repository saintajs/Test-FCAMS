using System;
using System.Collections.Generic;
using FCAMS.Web.DAL.Repositories;
using FCAMS.Web.Models;
using FCAMS.Web.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace FCAMS.Tests.Services
{
    /// <summary>
    /// Unit tests for the compliance rule engine -- the core business logic
    /// of FCAMS. Each rule type is tested for both its triggering case and
    /// its non-triggering case, using mocked repositories so no database is
    /// required to run this suite (see Docs/ARCHITECTURE.md, "Testing
    /// Strategy", for why the engine's constructor takes interfaces).
    /// </summary>
    [TestClass]
    public class ComplianceRuleEngineTests
    {
        private Mock<IComplianceRepository> _complianceRepo;
        private Mock<ITransactionRepository> _transactionRepo;
        private ComplianceRuleEngine _engine;

        [TestInitialize]
        public void Setup()
        {
            _complianceRepo = new Mock<IComplianceRepository>();
            _transactionRepo = new Mock<ITransactionRepository>();
            _engine = new ComplianceRuleEngine(_complianceRepo.Object, _transactionRepo.Object);
        }

        private static FinancialTransaction MakeTransaction(decimal amount = 500m, DateTime? date = null, string accountCode = "GL-1000", string status = "Pending", DateTime? createdDate = null)
        {
            return new FinancialTransaction
            {
                TransactionId = 1,
                TransactionRef = "TXN-TEST-001",
                TransactionDate = date ?? new DateTime(2026, 8, 17), // a Monday
                Amount = amount,
                AccountCode = accountCode,
                CurrencyCode = "USD",
                DepartmentId = 1,
                Status = status,
                CreatedByUserId = 1,
                CreatedDate = createdDate ?? DateTime.UtcNow
            };
        }

        private static ComplianceRule MakeRule(string ruleType, decimal? threshold = null, int? windowDays = null, string severity = "High")
        {
            return new ComplianceRule
            {
                RuleId = 1,
                RuleCode = "TEST-RULE",
                RuleName = "Test Rule",
                RuleType = ruleType,
                ThresholdValue = threshold,
                ThresholdWindowDays = windowDays,
                Severity = severity,
                IsActive = true
            };
        }

        // ---------- AmountThreshold ----------------------------------------

        [TestMethod]
        public void AmountThreshold_AmountExceedsThreshold_RaisesViolation()
        {
            var rule = MakeRule("AmountThreshold", threshold: 10000m);
            _complianceRepo.Setup(r => r.GetActiveRules()).Returns(new List<ComplianceRule> { rule });
            var txn = MakeTransaction(amount: 15000m);

            var results = _engine.EvaluateTransaction(txn);

            Assert.IsTrue(results[0].IsViolation);
            _complianceRepo.Verify(r => r.CreateViolation(txn.TransactionId, rule.RuleId, rule.Severity, It.IsAny<string>()), Times.Once);
        }

        [TestMethod]
        public void AmountThreshold_AmountBelowThreshold_NoViolation()
        {
            var rule = MakeRule("AmountThreshold", threshold: 10000m);
            _complianceRepo.Setup(r => r.GetActiveRules()).Returns(new List<ComplianceRule> { rule });
            var txn = MakeTransaction(amount: 500m);

            var results = _engine.EvaluateTransaction(txn);

            Assert.IsFalse(results[0].IsViolation);
            _complianceRepo.Verify(r => r.CreateViolation(It.IsAny<long>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [TestMethod]
        public void AmountThreshold_NegativeAmountExceedsThresholdInAbsoluteValue_RaisesViolation()
        {
            var rule = MakeRule("AmountThreshold", threshold: 10000m);
            _complianceRepo.Setup(r => r.GetActiveRules()).Returns(new List<ComplianceRule> { rule });
            var txn = MakeTransaction(amount: -15000m); // a large refund/credit

            var results = _engine.EvaluateTransaction(txn);

            Assert.IsTrue(results[0].IsViolation);
        }

        // ---------- DuplicateTransaction ------------------------------------

        [TestMethod]
        public void DuplicateTransaction_MatchingCandidateFound_RaisesViolation()
        {
            var rule = MakeRule("DuplicateTransaction", windowDays: 3);
            _complianceRepo.Setup(r => r.GetActiveRules()).Returns(new List<ComplianceRule> { rule });
            var txn = MakeTransaction(amount: 750m, accountCode: "GL-2000");

            _transactionRepo.Setup(r => r.FindDuplicateCandidates(txn.TransactionId, txn.AccountCode, txn.Amount, txn.TransactionDate, 3))
                .Returns(new List<(long, string, DateTime, decimal)> { (99, "TXN-OLD-099", txn.TransactionDate.AddDays(-1), 750m) });

            var results = _engine.EvaluateTransaction(txn);

            Assert.IsTrue(results[0].IsViolation);
            StringAssert.Contains(results[0].DetailMessage, "TXN-OLD-099");
        }

        [TestMethod]
        public void DuplicateTransaction_NoCandidatesFound_NoViolation()
        {
            var rule = MakeRule("DuplicateTransaction", windowDays: 3);
            _complianceRepo.Setup(r => r.GetActiveRules()).Returns(new List<ComplianceRule> { rule });
            var txn = MakeTransaction();

            _transactionRepo.Setup(r => r.FindDuplicateCandidates(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<DateTime>(), It.IsAny<int>()))
                .Returns(new List<(long, string, DateTime, decimal)>());

            var results = _engine.EvaluateTransaction(txn);

            Assert.IsFalse(results[0].IsViolation);
        }

        // ---------- RoundDollar ----------------------------------------------

        [TestMethod]
        public void RoundDollar_ExactMultipleOfOneThousandAboveMinimum_RaisesViolation()
        {
            var rule = MakeRule("RoundDollar", threshold: 1000m);
            _complianceRepo.Setup(r => r.GetActiveRules()).Returns(new List<ComplianceRule> { rule });
            var txn = MakeTransaction(amount: 5000.00m);

            var results = _engine.EvaluateTransaction(txn);

            Assert.IsTrue(results[0].IsViolation);
        }

        [TestMethod]
        public void RoundDollar_NonRoundAmount_NoViolation()
        {
            var rule = MakeRule("RoundDollar", threshold: 1000m);
            _complianceRepo.Setup(r => r.GetActiveRules()).Returns(new List<ComplianceRule> { rule });
            var txn = MakeTransaction(amount: 5123.47m);

            var results = _engine.EvaluateTransaction(txn);

            Assert.IsFalse(results[0].IsViolation);
        }

        [TestMethod]
        public void RoundDollar_RoundAmountBelowMinimumSize_NoViolation()
        {
            var rule = MakeRule("RoundDollar", threshold: 1000m);
            _complianceRepo.Setup(r => r.GetActiveRules()).Returns(new List<ComplianceRule> { rule });
            var txn = MakeTransaction(amount: 500.00m); // round, but under the $1000 minimum

            var results = _engine.EvaluateTransaction(txn);

            Assert.IsFalse(results[0].IsViolation);
        }

        // ---------- WeekendPosting -------------------------------------------

        [TestMethod]
        public void WeekendPosting_TransactionDatedSaturday_RaisesViolation()
        {
            var rule = MakeRule("WeekendPosting");
            _complianceRepo.Setup(r => r.GetActiveRules()).Returns(new List<ComplianceRule> { rule });
            var saturday = new DateTime(2026, 8, 22); // confirmed Saturday
            var txn = MakeTransaction(date: saturday);

            var results = _engine.EvaluateTransaction(txn);

            Assert.IsTrue(results[0].IsViolation);
        }

        [TestMethod]
        public void WeekendPosting_TransactionDatedWeekday_NoViolation()
        {
            var rule = MakeRule("WeekendPosting");
            _complianceRepo.Setup(r => r.GetActiveRules()).Returns(new List<ComplianceRule> { rule });
            var monday = new DateTime(2026, 8, 17);
            var txn = MakeTransaction(date: monday);

            var results = _engine.EvaluateTransaction(txn);

            Assert.IsFalse(results[0].IsViolation);
        }

        // ---------- MissingApproval -------------------------------------------

        [TestMethod]
        public void MissingApproval_HighValuePendingPastWindow_RaisesViolation()
        {
            var rule = MakeRule("MissingApproval", threshold: 25000m, windowDays: 2);
            _complianceRepo.Setup(r => r.GetActiveRules()).Returns(new List<ComplianceRule> { rule });
            var txn = MakeTransaction(amount: 30000m, status: "Pending", createdDate: DateTime.UtcNow.AddDays(-3));

            var results = _engine.EvaluateTransaction(txn);

            Assert.IsTrue(results[0].IsViolation);
        }

        [TestMethod]
        public void MissingApproval_HighValueButStillWithinWindow_NoViolation()
        {
            var rule = MakeRule("MissingApproval", threshold: 25000m, windowDays: 2);
            _complianceRepo.Setup(r => r.GetActiveRules()).Returns(new List<ComplianceRule> { rule });
            var txn = MakeTransaction(amount: 30000m, status: "Pending", createdDate: DateTime.UtcNow.AddHours(-1));

            var results = _engine.EvaluateTransaction(txn);

            Assert.IsFalse(results[0].IsViolation);
        }

        [TestMethod]
        public void MissingApproval_AlreadyApproved_NoViolation()
        {
            var rule = MakeRule("MissingApproval", threshold: 25000m, windowDays: 2);
            _complianceRepo.Setup(r => r.GetActiveRules()).Returns(new List<ComplianceRule> { rule });
            var txn = MakeTransaction(amount: 30000m, status: "Approved", createdDate: DateTime.UtcNow.AddDays(-5));

            var results = _engine.EvaluateTransaction(txn);

            Assert.IsFalse(results[0].IsViolation);
        }

        // ---------- VelocityCheck ---------------------------------------------

        [TestMethod]
        public void VelocityCheck_RecentCountExceedsMax_RaisesViolation()
        {
            var rule = MakeRule("VelocityCheck", threshold: 5m, windowDays: 1);
            _complianceRepo.Setup(r => r.GetActiveRules()).Returns(new List<ComplianceRule> { rule });
            var txn = MakeTransaction(accountCode: "GL-3000");

            _transactionRepo.Setup(r => r.CountRecentByAccount(txn.AccountCode, txn.TransactionDate, 1)).Returns(8);

            var results = _engine.EvaluateTransaction(txn);

            Assert.IsTrue(results[0].IsViolation);
        }

        [TestMethod]
        public void VelocityCheck_RecentCountWithinLimit_NoViolation()
        {
            var rule = MakeRule("VelocityCheck", threshold: 5m, windowDays: 1);
            _complianceRepo.Setup(r => r.GetActiveRules()).Returns(new List<ComplianceRule> { rule });
            var txn = MakeTransaction(accountCode: "GL-3000");

            _transactionRepo.Setup(r => r.CountRecentByAccount(txn.AccountCode, txn.TransactionDate, 1)).Returns(2);

            var results = _engine.EvaluateTransaction(txn);

            Assert.IsFalse(results[0].IsViolation);
        }

        // ---------- Cross-cutting behavior --------------------------------------

        [TestMethod]
        public void EvaluateTransaction_MultipleActiveRules_EvaluatesEachIndependently()
        {
            var rules = new List<ComplianceRule>
            {
                MakeRule("AmountThreshold", threshold: 100m),   // will trigger
                MakeRule("WeekendPosting")                       // will not trigger (Monday)
            };
            _complianceRepo.Setup(r => r.GetActiveRules()).Returns(rules);
            var txn = MakeTransaction(amount: 5000m, date: new DateTime(2026, 8, 17));

            var results = _engine.EvaluateTransaction(txn);

            Assert.AreEqual(2, results.Count);
            Assert.IsTrue(results[0].IsViolation);
            Assert.IsFalse(results[1].IsViolation);
            _complianceRepo.Verify(r => r.CreateViolation(It.IsAny<long>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        }

        [TestMethod]
        public void EvaluateTransaction_UnknownRuleType_DoesNotThrowAndDoesNotFlag()
        {
            var rule = MakeRule("SomeFutureRuleTypeNotYetImplemented");
            _complianceRepo.Setup(r => r.GetActiveRules()).Returns(new List<ComplianceRule> { rule });
            var txn = MakeTransaction();

            var results = _engine.EvaluateTransaction(txn);

            Assert.IsFalse(results[0].IsViolation);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void EvaluateTransaction_NullTransaction_ThrowsArgumentNullException()
        {
            _engine.EvaluateTransaction(null);
        }

        [TestMethod]
        public void EvaluateTransaction_NoActiveRules_ReturnsEmptyResultList()
        {
            _complianceRepo.Setup(r => r.GetActiveRules()).Returns(new List<ComplianceRule>());
            var txn = MakeTransaction();

            var results = _engine.EvaluateTransaction(txn);

            Assert.AreEqual(0, results.Count);
        }
    }
}
