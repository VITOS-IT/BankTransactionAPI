using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TransactionsMicroservice.Models
{
    public class TransactionContext:DbContext
    {
        public TransactionContext(DbContextOptions<TransactionContext> options):base(options)
        {

        }
        public DbSet<Transaction> Transactions { get; set; }
    }
}
