# ClearBank Developer Test — Refactored

## Where I started

The first thing I read was the README — it told me this was a refactoring exercise, not a feature build. The logic was to stay the same. The goal was to make it cleaner, more testable, and easier to reason about. Three things: SOLID principles, testability, readability.

From there I went through each file to understand what I was working with before touching anything.

`IPaymentService.cs` was the contract — one method, `MakePayment`, takes a request and returns a result. The brief said not to change this signature, so this became the boundary everything had to fit inside.

`PaymentService.cs` was where all the work was. The logic itself was sound — look up the account, check it can make this type of payment, deduct the balance, save it back. But the method was doing everything itself: reading from app config, deciding which database class to use, creating it with `new`, fetching the account, validating, then creating the database class all over again just to save the result. No way to test any of it without real config and a real database.

`AccountDataStore.cs` and `BackupAccountDataStore.cs` were the two data stores — one primary, one backup. Both had the exact same two methods but no shared type between them. They were essentially strangers with identical jobs.

Then the types. `MakePaymentRequest` told me what comes in — account numbers, amount, date, and which payment scheme. `MakePaymentResult` told me what goes out — just a boolean. `Account` told me what the data store returns — balance, status, and which payment schemes it's allowed to use. `AccountStatus` had three states: `Live`, `Disabled`, `InboundPaymentsOnly`. `PaymentScheme` had three values: `Bacs`, `FasterPayments`, `Chaps`. `AllowedPaymentSchemes` mirrored those three but as a flags enum using bit-shift values — except the `[Flags]` attribute was missing, which I noted for later.

By the time I'd read everything I had a clear picture of the business rules the code was protecting:

- Bacs only needs the scheme to be enabled on the account
- Faster Payments needs the scheme enabled and enough balance to cover the amount
- Chaps needs the scheme enabled and the account must be actively Live

Once I had that written down I wrote the tests before touching anything else. That way I had something to run against at each step, and I knew immediately if I'd broken a rule while moving things around.

## What I found

Reading through the code a handful of problems stood out, some bigger than others.

The most significant one was that `PaymentService` had no seams. It created its own data store instances using `new` directly inside the method, and it decided which one to create by reading from app config. That meant the method controlled everything — its own dependencies, its own configuration, its own database connections. There was no way to get in from the outside. You couldn't write a test without a real database and real config behind it.

Related to that, the store creation logic appeared twice in the same method — once to fetch the account at the top, and again to save it at the bottom. Two separate `if/else` blocks doing the exact same thing, four lines apart. If the store selection logic ever needed to change, you'd have to remember to change it in both places.

The two data stores themselves — `AccountDataStore` and `BackupAccountDataStore` — had identical method signatures but no shared interface. From the compiler's perspective they were completely unrelated types. That's what forced the duplication above, and it's what made mocking impossible.

Inside the switch statement the null account check was written separately inside every single case. Three cases, three identical `if (account == null)` blocks. If a fourth scheme was ever added, someone would have to remember to add it there too.

There was also no default case in the switch. If a payment scheme came in that didn't match Bacs, FasterPayments, or Chaps, the code would fall through silently. `Success` was set to `true` at the top and nothing would have changed it — so an unknown scheme would have been approved and money would have moved. In a payment system that's not a minor edge case, it's a real risk.

Finally, `AllowedPaymentSchemes` was written as a flags enum — the values are powers of two specifically so multiple schemes can be combined on one account — but the `[Flags]` attribute was missing. The validation still worked, but any log or debug output showing an account's allowed schemes would print a raw number rather than readable names.

## What I changed and why

Before touching a single line of `PaymentService` I wrote the tests first.

The reason is simple — the tests forced me to prove I actually understood the rules before I started moving things around. Each test is a statement of what the code should do: if the account doesn't exist the payment must fail, if Chaps is requested the account must be Live, if the balance is too low Faster Payments must be rejected. Writing those down as tests before touching the implementation meant I had a safety net from the very first change. If I broke a rule mid-refactor, something would go red immediately rather than me finding out later.

To even write the tests I needed `IAccountDataStore` to exist first — the tests create a mock of it and pass it into `PaymentService`, so the interface had to be there before the test file could compile. That's the only reason it came before the tests rather than after. Everything else — the changes to `PaymentService`, the stores implementing the interface, the enum fix — came after the tests were written and failing.

Once the tests were in place and failing for the right reasons, the refactoring had a clear target.

The single biggest change was making `PaymentService` accept the data store through its constructor instead of creating it internally with `new`. The service now says "give me something that can fetch and update accounts" and whoever builds it decides which store to provide. In production that's the DI container. In tests that's the mock — which is exactly why the tests could now run without a real database behind them. That one change is what made everything else possible.

To make that work `AccountDataStore` and `BackupAccountDataStore` both needed to implement `IAccountDataStore`. Once they did, the service could hold a single reference and use it for both the fetch and the update — which also removed the duplicated store-creation block that appeared twice in the original method.

Inside `MakePayment` the null account check came out of each switch case and became a single guard at the top. The switch itself became a switch expression calling a private method per scheme, so each scheme's rules live in one clearly named place rather than buried inside a case block. The missing default case got a `_ => false` to close off the silent approval bug.

The `[Flags]` attribute went onto `AllowedPaymentSchemes` — small change, but it makes the enum's intent explicit and fixes how it prints in logs.

By the end all 24 tests were green and nothing in the business logic had changed — just how it was structured and how it could be verified.

## What I'd tackle next

The private validation methods work and are readable, but they're still sitting inside `PaymentService`. That means adding a new payment scheme in the future means opening a file that's already working and adding to it. The cleaner long-term answer is a separate validator class per scheme, each implementing an `IPaymentValidator` interface, registered in a dictionary by scheme type. New scheme, new class, nothing existing gets touched.

The data store selection — which store to use, primary or backup — is currently handled at the DI container level which is the right place for it. But it would be worth replacing the raw config string with a proper `IOptions<DataStoreSettings>` class so the configuration is typed and validated at startup rather than being a magic string that fails silently.

The tests cover the happy and unhappy paths for each scheme, input validation, and edge cases like zero and negative amounts. What's still missing is a test for combined flag values — an account with multiple schemes enabled — and an integration test that runs the full flow against a real or in-memory store rather than a mock.
