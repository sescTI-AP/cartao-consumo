﻿namespace Cielo
{
    public class Transaction
    {
        public Transaction()
        {
        }

        public Transaction(string merchantOrderId, Customer customer, Payment payment)
        {
            this.MerchantOrderId = merchantOrderId;
            this.Customer = customer;
            this.Payment = payment;
        }

        public string MerchantOrderId { get; set; }
        public Customer Customer { get; set; }
        public Payment Payment { get; set; }
    }
}
