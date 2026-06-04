namespace EcaInformationSystem.Domain.Exceptions
{
    public class ConcurrencyException : Exception
    {
         public ConcurrencyException()
            : base("This record was modified by another user while you were editing it. " +
                   "Please reload the record and apply your changes again.")
        {
        }

        public ConcurrencyException(string message)
            : base(message)
        {
        }

        public ConcurrencyException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}