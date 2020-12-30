using Core;
using Persistence;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace API
{
    public class DataSeeder
    {
        private readonly DataContext context;

        public DataSeeder(DataContext context)
        {
            this.context = context;
        }

        public void SeedContacts()
        {

            var contacts = new List<Contact>();
            contacts.Add(new Contact() { Name = "Randel Ramirez", Address = "Alabang" });
            contacts.Add(new Contact() { Name = "LeBron James", Address = "LA" });
            contacts.Add(new Contact() { Name = "Kyrie Irving", Address = "BK" });

            this.context.AddRange(contacts);
            this.context.SaveChanges();
        }
    }
}
