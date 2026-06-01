using System;

namespace SampleShared
{
    public class OrderCreatedEvent
    {
        public Guid OrderId { get; set; }
        public string Customer { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public DateTime CreatedUtc { get; set; }
        public bool ForceRollback { get; set; }
    }
}
