using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Core.ViewModels
{
    public class ContactViewModel
    {
        public Guid Id { get; set; }

        public string Name { get; set; }

        public string Address { get; set; }
    }
}
