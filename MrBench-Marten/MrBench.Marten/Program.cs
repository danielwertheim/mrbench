using System;
using System.Linq;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Marten;
using Marten.Services;

namespace MrBench.Marten
{
    class Program
    {
        static void Main(string[] args)
        {
            BenchmarkRunner.Run<AccountTransactions>();
        }
    }

    public class AccountTransactions
    {
        private static readonly Random Rnd = new Random();

        private IDocumentStore _store;

        [Setup]
        public void Setup()
        {
            _store = DocumentStore.For(_ =>
            {
                _.Connection("host=ubuntu01;database=foo;password=asdf;username=postgres");

                _.Schema.For<AccountInfoView>().Index(i => i.No);
                _.Schema.For<AccountInfoView>().Index(i => i.OwnerId);

                _.Events.InlineProjections.AggregateStreamsWith<AccountInfoView>();

                _.Events.AddEventType(typeof(AccountOpened));
                _.Events.AddEventType(typeof(AccountCredited));
                _.Events.AddEventType(typeof(AccountDebited));
            });

            _store.Advanced.Clean.DeleteAllDocuments();
            _store.Advanced.Clean.DeleteAllEventData();
        }

        [Benchmark]
        public void OpenTwoAccountsAndTransferSomeMoney()
        {

            var id1 = Guid.NewGuid();
            var id2 = Guid.NewGuid();

            var account1Owner = Guid.NewGuid().ToString("n");
            var account2Owner = Guid.NewGuid().ToString("n");

            using (var session = _store.LightweightSession())
            {
                session.Events.StartStream<Account>(id1, new AccountOpened
                {
                    No = 1,
                    OpenedAt = DateTime.UtcNow,
                    OwnerId = account1Owner
                });

                session.Events.StartStream<Account>(id2, new AccountOpened
                {
                    No = 2,
                    OpenedAt = DateTime.UtcNow,
                    OwnerId = account2Owner
                });
                session.SaveChanges();
            }

            using (var session = _store.LightweightSession())
            {
                var account1 = session.Events.AggregateStream<Account>(id1);

                var events = account1.TransferTo(id2, Rnd.Next(1, 500));

                session.Events.Append(id1, events.Item1);
                session.Events.Append(id2, events.Item2);

                session.SaveChanges();
            }

            using (var session = _store.QuerySession(new SessionOptions { Tracking = DocumentTracking.None }))
            {
                var account1 = session.Query<AccountInfoView>().Where(a => a.OwnerId == account1Owner).Single();
                var account2 = session.Query<AccountInfoView>().Where(a => a.OwnerId == account2Owner).Single();
            }
        }
    }

    public class Account
    {
        public Guid Id { get; set; }
        public long No { get; set; }
        public string OwnerId { get; set; }
        public DateTime OpenedAt { get; set; }
        public decimal Balance { get; set; }

        public void Apply(AccountOpened ev)
        {
            No = ev.No;
            OwnerId = ev.OwnerId;
            OpenedAt = ev.OpenedAt;
        }

        public void Apply(AccountCredited ev)
            => Balance += ev.Amount;

        public void Apply(AccountDebited ev)
            => Balance -= ev.Amount;

        public Tuple<AccountCredited, AccountDebited> TransferTo(Guid targetAccountId, decimal amount)
            => new Tuple<AccountCredited, AccountDebited>(
                new AccountCredited
                {
                    AccountId = Id,
                    Amount = amount,
                    TransactionAt = DateTime.UtcNow
                },
                new AccountDebited
                {
                    AccountId = targetAccountId,
                    Amount = amount,
                    TransactionAt = DateTime.UtcNow
                });
    }

    public class AccountInfoView
    {
        public Guid Id { get; set; }
        public long No { get; set; }
        public string OwnerId { get; set; }
        public DateTime OpenedAt { get; set; }
        public DateTime LastTransactionAt { get; set; }
        public decimal Balance { get; set; }

        public void Apply(AccountOpened ev)
        {
            No = ev.No;
            OwnerId = ev.OwnerId;
            OpenedAt = ev.OpenedAt;
        }

        public void Apply(AccountCredited ev)
        {
            Balance += ev.Amount;
            LastTransactionAt = ev.TransactionAt;
        }

        public void Apply(AccountDebited ev)
        {
            Balance -= ev.Amount;
            LastTransactionAt = ev.TransactionAt;
        }
    }

    public class AccountOpened
    {
        public long No { get; set; }
        public string OwnerId { get; set; }
        public DateTime OpenedAt { get; set; }
    }

    public class AccountCredited
    {
        public Guid AccountId { get; set; }
        public decimal Amount { get; set; }
        public DateTime TransactionAt { get; set; }
    }

    public class AccountDebited
    {
        public Guid AccountId { get; set; }
        public decimal Amount { get; set; }
        public DateTime TransactionAt { get; set; }
    }
}
