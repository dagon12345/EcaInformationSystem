namespace EcaInformationSystem.Domain.Entities
{
    public class Product
    {
        public Guid Id { get; private set; }
        public string Name { get; private set; } = string.Empty;
        public decimal Price { get; private set; }
        private Product() { } // EF Core
        public Product(string name, decimal price)
        {
            Id = Guid.NewGuid();
            Update(name, price);
        }
        public void Update(string name, decimal price)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Name cannot be empty.");
            if (price <= 0)
                throw new ArgumentException("Price must be greater than zero.");
            Name = name.Trim();
            Price = price;
        }
    }

}
