using ClearBank.DeveloperTest.Types;

namespace ClearBank.DeveloperTest.Data
{
    // Shared contract for both account data stores.
    // PaymentService depends on this interface, not on either concrete class,
    // so the active store can be swapped or mocked without touching the service.
    public interface IAccountDataStore
    {
        Account GetAccount(string accountNumber);
        void UpdateAccount(Account account);
    }
}
