using System;
using System.Collections.Generic;
using System.Linq;
using FCAMS.Web.DAL.Repositories;
using FCAMS.Web.Models;

namespace FCAMS.Web.Services
{
    /// <summary>Result of evaluating one rule against one transaction.</summary>
    public class RuleEvaluationResult
    {
        public bool IsViolation { get; }
        public ComplianceRule Rule { get; }
        public string DetailMessage { get; }

        public RuleEvaluationResult(bool isViolation, ComplianceRule rule, string detailMessage = null)
        {
            IsViolation = isViolation;
            Rule = rule;
            DetailMessage = detailMessage;
        }
    }

    public interface IComplianceRuleEngine
    {
        /// <summary>
        /// Evaluates every active rule against the given transaction,
        /// persists a ComplianceViolations row for each breach found, and
        /// returns the list of violations raised.
        /// </summary>
        List<RuleEvaluationResult> EvaluateTransaction(FinancialTransaction transaction);
    }

    /// <summary>
    /// The rule engine is intentionally data-driven: ComplianceRules rows
    /// describe *which* rules are active and their thresholds, while this
    /// class holds the *evaluation logic* for each known RuleType. Adding a
    /// new tunable rule (different threshold, different severity, on/off)
    /// never requires a code change or redeploy -- only a row update via the
    /// Admin > Rules screen. Adding an entirely new *kind* of check does
    /// require a code change here, which is the correct boundary: business
    /// logic for "what counts as suspicious" belongs in source control and
    /// under unit test, not hidden in a database row as a string of SQL or
    /// a serialized expression tree.
    /// </summary>
    public class ComplianceRuleEngine : IComplianceRuleEngine
    {
        private readonly IComplianceRepository _complianceRepository;
        private readonly ITransactionRepository _transactionRepository;

        public ComplianceRuleEngine(IComplianceRepository complianceRepository, ITransactionRepository transactionRepository)
        {
            _complianceRepository = complianceRepository ?? throw new ArgumentNullException(nameof(complianceRepository));
            _transactionRepository = transactionRepository ?? throw new ArgumentNullException(nameof(transactionRepository));
        }

        public List<RuleEvaluationResult> EvaluateTransaction(FinancialTransaction transaction)
        {
            if (transaction == null) throw new ArgumentNullException(nameof(transaction));

            var activeRules = _complianceRepository.GetActiveRules();
            var results = new List<RuleEvaluationResult>();

            foreach (var rule in activeRules)
            {
                var result = EvaluateSingleRule(rule, transaction);
                results.Add(result);

                if (result.IsViolation)
                {
                    _complianceRepository.CreateViolation(transaction.TransactionId, rule.RuleId, rule.Severity, result.DetailMessage);
                }
            }

            return results;
        }

        private RuleEvaluationResult EvaluateSingleRule(ComplianceRule rule, FinancialTransaction txn)
        {
            switch (rule.RuleType)
            {
                case "AmountThreshold":
                    return EvaluateAmountThreshold(rule, txn);

                case "DuplicateTransaction":
                    return EvaluateDuplicateTransaction(rule, txn);

                case "RoundDollar":
                    return EvaluateRoundDollar(rule, txn);

                case "WeekendPosting":
                    return EvaluateWeekendPosting(rule, txn);

                case "MissingApproval":
                    return EvaluateMissingApproval(rule, txn);

                case "VelocityCheck":
                    return EvaluateVelocityCheck(rule, txn);

                default:
                    // Unknown rule type: fail safe by not flagging, rather
                    // than throwing and blocking every transaction because
                    // of one misconfigured row.
                    return new RuleEvaluationResult(false, rule);
            }
        }

        /// <summary>RULE-001: single transaction exceeds an absolute dollar threshold.</summary>
        private RuleEvaluationResult EvaluateAmountThreshold(ComplianceRule rule, FinancialTransaction txn)
        {
            if (!rule.ThresholdValue.HasValue) return new RuleEvaluationResult(false, rule);

            decimal absAmount = Math.Abs(txn.Amount);
            if (absAmount > rule.ThresholdValue.Value)
            {
                string message = $"Transaction amount {txn.Amount:C} exceeds the configured threshold of {rule.ThresholdValue.Value:C}.";
                return new RuleEvaluationResult(true, rule, message);
            }
            return new RuleEvaluationResult(false, rule);
        }

        /// <summary>RULE-002: another transaction on the same account, same amount, within N days.</summary>
        private RuleEvaluationResult EvaluateDuplicateTransaction(ComplianceRule rule, FinancialTransaction txn)
        {
            int windowDays = rule.ThresholdWindowDays ?? 3;
            var candidates = _transactionRepository.FindDuplicateCandidates(
                txn.TransactionId, txn.AccountCode, txn.Amount, txn.TransactionDate, windowDays);

            if (candidates.Count > 0)
            {
                var match = candidates.First();
                string message = $"Possible duplicate of transaction {match.TransactionRef} " +
                                  $"(same account {txn.AccountCode}, same amount {txn.Amount:C}, dated {match.TransactionDate:d} -- within {windowDays} day window).";
                return new RuleEvaluationResult(true, rule, message);
            }
            return new RuleEvaluationResult(false, rule);
        }

        /// <summary>RULE-003: suspiciously round amount (e.g. exactly $5,000.00) above a minimum size.</summary>
        private RuleEvaluationResult EvaluateRoundDollar(ComplianceRule rule, FinancialTransaction txn)
        {
            decimal minSize = rule.ThresholdValue ?? 1000m;
            decimal absAmount = Math.Abs(txn.Amount);

            bool isRound = absAmount >= minSize && (absAmount % 1000m == 0m);
            if (isRound)
            {
                string message = $"Amount {txn.Amount:C} is an exact multiple of $1,000, a common indicator of an estimated or fabricated entry.";
                return new RuleEvaluationResult(true, rule, message);
            }
            return new RuleEvaluationResult(false, rule);
        }

        /// <summary>RULE-004: transaction dated on a Saturday or Sunday.</summary>
        private RuleEvaluationResult EvaluateWeekendPosting(ComplianceRule rule, FinancialTransaction txn)
        {
            var dayOfWeek = txn.TransactionDate.DayOfWeek;
            if (dayOfWeek == DayOfWeek.Saturday || dayOfWeek == DayOfWeek.Sunday)
            {
                string message = $"Transaction is dated {txn.TransactionDate:D} ({dayOfWeek}), outside normal business-day processing.";
                return new RuleEvaluationResult(true, rule, message);
            }
            return new RuleEvaluationResult(false, rule);
        }

        /// <summary>
        /// RULE-005: high-value transaction that has sat in Pending status
        /// longer than the allowed window. Evaluated at creation time using
        /// the transaction's own age is meaningless (it's brand new), so
        /// this rule is designed to also be re-run by a scheduled sweep
        /// (see Docs/ARCHITECTURE.md, "Batch Compliance Sweep") over all
        /// Pending transactions -- at creation time it only checks whether
        /// the transaction *starts* above the value threshold, tagging it
        /// for that later sweep by virtue of the AmountThreshold /
        /// MissingApproval combination.
        /// </summary>
        private RuleEvaluationResult EvaluateMissingApproval(ComplianceRule rule, FinancialTransaction txn)
        {
            if (!rule.ThresholdValue.HasValue) return new RuleEvaluationResult(false, rule);

            int allowedDays = rule.ThresholdWindowDays ?? 2;
            decimal absAmount = Math.Abs(txn.Amount);
            bool aboveValueThreshold = absAmount > rule.ThresholdValue.Value;
            bool stillPending = string.Equals(txn.Status, "Pending", StringComparison.OrdinalIgnoreCase);
            bool overdue = (DateTime.UtcNow - txn.CreatedDate).TotalDays > allowedDays;

            if (aboveValueThreshold && stillPending && overdue)
            {
                string message = $"High-value transaction ({txn.Amount:C}) has remained Pending for more than {allowedDays} day(s) without approval.";
                return new RuleEvaluationResult(true, rule, message);
            }
            return new RuleEvaluationResult(false, rule);
        }

        /// <summary>RULE-006: unusually high transaction count on one account within a short window.</summary>
        private RuleEvaluationResult EvaluateVelocityCheck(ComplianceRule rule, FinancialTransaction txn)
        {
            int windowDays = rule.ThresholdWindowDays ?? 1;
            int maxAllowed = rule.ThresholdValue.HasValue ? (int)rule.ThresholdValue.Value : 5;

            int recentCount = _transactionRepository.CountRecentByAccount(txn.AccountCode, txn.TransactionDate, windowDays);
            if (recentCount > maxAllowed)
            {
                string message = $"Account {txn.AccountCode} has posted {recentCount} transactions within {windowDays} day(s), exceeding the velocity threshold of {maxAllowed}.";
                return new RuleEvaluationResult(true, rule, message);
            }
            return new RuleEvaluationResult(false, rule);
        }
    }
}
