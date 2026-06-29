using ClearBank.DeveloperTest.Data;
using ClearBank.DeveloperTest.Types;

namespace ClearBank.DeveloperTest.Services
{
    public class PaymentService : IPaymentService
    {
        private readonly IAccountDataStore _accountDataStore;

        // The data store is injected so PaymentService doesn't decide which store to use
        // or how to create it. That decision lives at the composition root (e.g. Program.cs),
        // where config is read and the right implementation is registered with the DI container.
        // This also means the store can be replaced with a mock in tests.
        public PaymentService(IAccountDataStore accountDataStore)
        {
            _accountDataStore = accountDataStore;
        }

        public MakePaymentResult MakePayment(MakePaymentRequest request)
        {
            if (request == null)
                return new MakePaymentResult { Success = false };

            if (string.IsNullOrWhiteSpace(request.DebtorAccountNumber))
                return new MakePaymentResult { Success = false };

            if (request.Amount <= 0)
                return new MakePaymentResult { Success = false };

            var account = _accountDataStore.GetAccount(request.DebtorAccountNumber);

            // Guard checked once here rather than duplicated inside every scheme's branch.
            if (account == null)
                return new MakePaymentResult { Success = false };

            var isValid = request.PaymentScheme switch
            {
                PaymentScheme.Bacs           => IsBacsPaymentValid(account),
                PaymentScheme.FasterPayments => IsFasterPaymentsPaymentValid(account, request),
                PaymentScheme.Chaps          => IsChapsPaymentValid(account),
                _                            => false // unknown scheme — reject safely
            };

            if (!isValid)
                return new MakePaymentResult { Success = false };

            account.Balance -= request.Amount;
            _accountDataStore.UpdateAccount(account);

            return new MakePaymentResult { Success = true };
        }

        // Bacs only requires the scheme to be enabled on the account.
        private static bool IsBacsPaymentValid(Account account) =>
            account.AllowedPaymentSchemes.HasFlag(AllowedPaymentSchemes.Bacs);

        // Faster Payments requires the scheme to be enabled and sufficient funds.
        private static bool IsFasterPaymentsPaymentValid(Account account, MakePaymentRequest request) =>
            account.AllowedPaymentSchemes.HasFlag(AllowedPaymentSchemes.FasterPayments)
            && account.Balance >= request.Amount;

        // Chaps requires the scheme to be enabled and the account to be actively live.
        private static bool IsChapsPaymentValid(Account account) =>
            account.AllowedPaymentSchemes.HasFlag(AllowedPaymentSchemes.Chaps)
            && account.Status == AccountStatus.Live;
    }
}
