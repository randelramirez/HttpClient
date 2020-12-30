using Core;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace Persistence
{
    public class DataContext : DbContext
    {
        public DbSet<Contact> Contacts { get; set; }

        public DataContext(DbContextOptions<DataContext> options) : base(options)
        {
        }
    }
}
