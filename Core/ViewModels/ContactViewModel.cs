using System;

namespace Core.ViewModels
{
    public class ContactViewModel
    {
        public Guid Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string Address { get; set; } = string.Empty;
    }
}
