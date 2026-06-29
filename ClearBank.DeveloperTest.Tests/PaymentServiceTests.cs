using System;
using ClearBank.DeveloperTest.Data;
using ClearBank.DeveloperTest.Services;
using ClearBank.DeveloperTest.Types;
using Moq;
using Xunit;

namespace ClearBank.DeveloperTest.Tests
{
    public class PaymentServiceTests
    {
        private readonly Mock<IAccountDataStore> _accountDataStoreMock;
        private readonly PaymentService _paymentService;

        public PaymentServiceTests()
        {
            _accountDataStoreMock = new Mock<IAccountDataStore>();
            _paymentService = new PaymentService(_accountDataStoreMock.Object);
        }

        // Returns an account with sensible defaults — override only what each test cares about.
        private static Account CreateAccount(
            AllowedPaymentSchemes allowedSchemes = AllowedPaymentSchemes.Bacs,
            decimal balance = 1000m,
            AccountStatus status = AccountStatus.Live)
        {
            return new Account
            {
                AccountNumber = "12345",
                Balance = balance,
                Status = status,
                AllowedPaymentSchemes = allowedSchemes
            };
        }

        private static MakePaymentRequest CreateRequest(
            PaymentScheme scheme = PaymentScheme.Bacs,
            decimal amount = 100m)
        {
            return new MakePaymentRequest
            {
                DebtorAccountNumber = "12345",
                CreditorAccountNumber = "67890",
                Amount = amount,
                PaymentDate = DateTime.UtcNow,
                PaymentScheme = scheme
            };
        }

        // ── Account not found ───────────────────────────────────────────────

        [Theory]
        [InlineData(PaymentScheme.Bacs)]
        [InlineData(PaymentScheme.FasterPayments)]
        [InlineData(PaymentScheme.Chaps)]
        public void MakePayment_ReturnsFailure_WhenAccountNotFound(PaymentScheme scheme)
        {
            _accountDataStoreMock
                .Setup(x => x.GetAccount(It.IsAny<string>()))
                .Returns((Account)null);

            var result = _paymentService.MakePayment(CreateRequest(scheme));

            Assert.False(result.Success);
        }

        [Theory]
        [InlineData(PaymentScheme.Bacs)]
        [InlineData(PaymentScheme.FasterPayments)]
        [InlineData(PaymentScheme.Chaps)]
        public void MakePayment_DoesNotUpdateAccount_WhenAccountNotFound(PaymentScheme scheme)
        {
            _accountDataStoreMock
                .Setup(x => x.GetAccount(It.IsAny<string>()))
                .Returns((Account)null);

            _paymentService.MakePayment(CreateRequest(scheme));

            _accountDataStoreMock.Verify(x => x.UpdateAccount(It.IsAny<Account>()), Times.Never);
        }

        // ── Bacs ────────────────────────────────────────────────────────────

        [Fact]
        public void MakePayment_Bacs_ReturnsSuccess_WhenBacsIsAllowed()
        {
            var account = CreateAccount(allowedSchemes: AllowedPaymentSchemes.Bacs);
            _accountDataStoreMock.Setup(x => x.GetAccount(It.IsAny<string>())).Returns(account);

            var result = _paymentService.MakePayment(CreateRequest(PaymentScheme.Bacs));

            Assert.True(result.Success);
        }

        [Fact]
        public void MakePayment_Bacs_ReturnsFailure_WhenBacsIsNotAllowed()
        {
            var account = CreateAccount(allowedSchemes: AllowedPaymentSchemes.FasterPayments);
            _accountDataStoreMock.Setup(x => x.GetAccount(It.IsAny<string>())).Returns(account);

            var result = _paymentService.MakePayment(CreateRequest(PaymentScheme.Bacs));

            Assert.False(result.Success);
        }

        // ── FasterPayments ──────────────────────────────────────────────────

        [Fact]
        public void MakePayment_FasterPayments_ReturnsSuccess_WhenAllowed_AndSufficientBalance()
        {
            var account = CreateAccount(allowedSchemes: AllowedPaymentSchemes.FasterPayments, balance: 500m);
            _accountDataStoreMock.Setup(x => x.GetAccount(It.IsAny<string>())).Returns(account);

            var result = _paymentService.MakePayment(CreateRequest(PaymentScheme.FasterPayments, amount: 100m));

            Assert.True(result.Success);
        }

        [Fact]
        public void MakePayment_FasterPayments_ReturnsFailure_WhenNotAllowed()
        {
            var account = CreateAccount(allowedSchemes: AllowedPaymentSchemes.Bacs, balance: 500m);
            _accountDataStoreMock.Setup(x => x.GetAccount(It.IsAny<string>())).Returns(account);

            var result = _paymentService.MakePayment(CreateRequest(PaymentScheme.FasterPayments, amount: 100m));

            Assert.False(result.Success);
        }

        [Fact]
        public void MakePayment_FasterPayments_ReturnsFailure_WhenInsufficientBalance()
        {
            var account = CreateAccount(allowedSchemes: AllowedPaymentSchemes.FasterPayments, balance: 50m);
            _accountDataStoreMock.Setup(x => x.GetAccount(It.IsAny<string>())).Returns(account);

            var result = _paymentService.MakePayment(CreateRequest(PaymentScheme.FasterPayments, amount: 100m));

            Assert.False(result.Success);
        }

        // ── Chaps ────────────────────────────────────────────────────────────

        [Fact]
        public void MakePayment_Chaps_ReturnsSuccess_WhenAllowed_AndAccountIsLive()
        {
            var account = CreateAccount(allowedSchemes: AllowedPaymentSchemes.Chaps, status: AccountStatus.Live);
            _accountDataStoreMock.Setup(x => x.GetAccount(It.IsAny<string>())).Returns(account);

            var result = _paymentService.MakePayment(CreateRequest(PaymentScheme.Chaps));

            Assert.True(result.Success);
        }

        [Fact]
        public void MakePayment_Chaps_ReturnsFailure_WhenNotAllowed()
        {
            var account = CreateAccount(allowedSchemes: AllowedPaymentSchemes.Bacs, status: AccountStatus.Live);
            _accountDataStoreMock.Setup(x => x.GetAccount(It.IsAny<string>())).Returns(account);

            var result = _paymentService.MakePayment(CreateRequest(PaymentScheme.Chaps));

            Assert.False(result.Success);
        }

        [Fact]
        public void MakePayment_Chaps_ReturnsFailure_WhenAccountIsDisabled()
        {
            var account = CreateAccount(allowedSchemes: AllowedPaymentSchemes.Chaps, status: AccountStatus.Disabled);
            _accountDataStoreMock.Setup(x => x.GetAccount(It.IsAny<string>())).Returns(account);

            var result = _paymentService.MakePayment(CreateRequest(PaymentScheme.Chaps));

            Assert.False(result.Success);
        }

        [Fact]
        public void MakePayment_Chaps_ReturnsFailure_WhenAccountIsInboundPaymentsOnly()
        {
            var account = CreateAccount(allowedSchemes: AllowedPaymentSchemes.Chaps, status: AccountStatus.InboundPaymentsOnly);
            _accountDataStoreMock.Setup(x => x.GetAccount(It.IsAny<string>())).Returns(account);

            var result = _paymentService.MakePayment(CreateRequest(PaymentScheme.Chaps));

            Assert.False(result.Success);
        }

        // ── Balance and update behaviour ────────────────────────────────────

        [Fact]
        public void MakePayment_DeductsPaymentAmount_FromAccountBalance_OnSuccess()
        {
            var account = CreateAccount(allowedSchemes: AllowedPaymentSchemes.Bacs, balance: 500m);
            _accountDataStoreMock.Setup(x => x.GetAccount(It.IsAny<string>())).Returns(account);

            _paymentService.MakePayment(CreateRequest(PaymentScheme.Bacs, amount: 100m));

            Assert.Equal(400m, account.Balance);
        }

        [Fact]
        public void MakePayment_CallsUpdateAccount_OnSuccess()
        {
            var account = CreateAccount(allowedSchemes: AllowedPaymentSchemes.Bacs);
            _accountDataStoreMock.Setup(x => x.GetAccount(It.IsAny<string>())).Returns(account);

            _paymentService.MakePayment(CreateRequest(PaymentScheme.Bacs));

            _accountDataStoreMock.Verify(x => x.UpdateAccount(account), Times.Once);
        }

        [Fact]
        public void MakePayment_DoesNotCallUpdateAccount_OnFailure()
        {
            // Bacs not in allowed schemes — payment should fail without touching the store.
            var account = CreateAccount(allowedSchemes: AllowedPaymentSchemes.FasterPayments);
            _accountDataStoreMock.Setup(x => x.GetAccount(It.IsAny<string>())).Returns(account);

            _paymentService.MakePayment(CreateRequest(PaymentScheme.Bacs));

            _accountDataStoreMock.Verify(x => x.UpdateAccount(It.IsAny<Account>()), Times.Never);
        }

        [Fact]
        public void MakePayment_DoesNotDeductBalance_OnFailure()
        {
            // Bacs not in allowed schemes — balance should remain untouched.
            var account = CreateAccount(allowedSchemes: AllowedPaymentSchemes.FasterPayments, balance: 500m);
            _accountDataStoreMock.Setup(x => x.GetAccount(It.IsAny<string>())).Returns(account);

            _paymentService.MakePayment(CreateRequest(PaymentScheme.Bacs, amount: 100m));

            Assert.Equal(500m, account.Balance);
        }

        // ── Unknown scheme ───────────────────────────────────────────────────

        [Fact]
        public void MakePayment_ReturnsFailure_WhenPaymentSchemeIsUnknown()
        {
            // Casting an arbitrary int to PaymentScheme simulates a value outside the known cases.
            var account = CreateAccount();
            _accountDataStoreMock.Setup(x => x.GetAccount(It.IsAny<string>())).Returns(account);

            var result = _paymentService.MakePayment(CreateRequest((PaymentScheme)99));

            Assert.False(result.Success);
        }

        // ── Input validation ─────────────────────────────────────────────────

        [Fact]
        public void MakePayment_ReturnsFailure_WhenDebtorAccountNumberIsNull()
        {
            var result = _paymentService.MakePayment(new MakePaymentRequest
            {
                DebtorAccountNumber = null,
                Amount = 100m,
                PaymentScheme = PaymentScheme.Bacs,
                PaymentDate = DateTime.UtcNow
            });

            Assert.False(result.Success);
        }

        [Fact]
        public void MakePayment_ReturnsFailure_WhenDebtorAccountNumberIsEmpty()
        {
            var result = _paymentService.MakePayment(new MakePaymentRequest
            {
                DebtorAccountNumber = "",
                Amount = 100m,
                PaymentScheme = PaymentScheme.Bacs,
                PaymentDate = DateTime.UtcNow
            });

            Assert.False(result.Success);
        }

        [Fact]
        public void MakePayment_ReturnsFailure_WhenAmountIsZero()
        {
            var account = CreateAccount(allowedSchemes: AllowedPaymentSchemes.Bacs);
            _accountDataStoreMock.Setup(x => x.GetAccount(It.IsAny<string>())).Returns(account);

            var result = _paymentService.MakePayment(CreateRequest(PaymentScheme.Bacs, amount: 0m));

            Assert.False(result.Success);
        }

        [Fact]
        public void MakePayment_ReturnsFailure_WhenAmountIsNegative()
        {
            var account = CreateAccount(allowedSchemes: AllowedPaymentSchemes.Bacs);
            _accountDataStoreMock.Setup(x => x.GetAccount(It.IsAny<string>())).Returns(account);

            var result = _paymentService.MakePayment(CreateRequest(PaymentScheme.Bacs, amount: -50m));

            Assert.False(result.Success);
        }
    }
}
