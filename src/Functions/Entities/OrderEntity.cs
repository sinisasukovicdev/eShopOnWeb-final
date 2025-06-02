using Functions.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Functions.Entities
{
    public class OrderEntity
    {
        public string id { get; private set; } = Guid.NewGuid().ToString();
        public DateTimeOffset OrderDate { get; set; }
        public Address ShipToAddress { get; set; }
        public IReadOnlyCollection<OrderItem> OrderItems { get; set; }
        public decimal FinalPrice { get; set; }
    }
}
