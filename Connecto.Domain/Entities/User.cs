namespace Connecto.Domain.Entities
{
    /// <summary>
    ///     user record
    /// </summary>
    /// <param name="firstName"></param>
    /// <param name="middleName"></param>
    /// <param name="lastName"></param>
    /// <param name="email"></param>
    /// <param name="address"></param>
    /// <param name="bio"></param>
    public sealed record User
    {
        public User() { }

        public User(string firstName, string middleName, string lastName, string email, string address, string city, string country, string bio) 
        {
            Id = Guid.NewGuid();
            FirstName = firstName;
            MiddleName = middleName;
            LastName = lastName;
            Email = email;
            Address = address;
            City = city;
            Country = country;
            Bio = bio;
        }

        /// <summary>
        ///     user id
        /// </summary>
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>
        ///     first name
        /// </summary>
        public string FirstName { get; set; }

        /// <summary>
        ///     middle name
        /// </summary>
        public string MiddleName { get; set; }

        /// <summary>
        ///     last name
        /// </summary>
        public string LastName { get; set; }

        /// <summary>
        ///     email
        /// </summary>
        public string Email { get; set; }

        /// <summary>
        ///     address
        /// </summary>
        public string Address { get; set; }

        /// <summary>
        ///     city
        /// </summary>
        public string City { get; set; }

        /// <summary>
        ///     country
        /// </summary>
        public string Country { get; set; }

        /// <summary>
        ///     bio
        /// </summary>
        public string Bio { get; set; }

        /// <summary>
        ///     verification status
        /// </summary>
        public VerificationStatus VerificationStatus { get; set; } = VerificationStatus.Unverified;

        /// <summary>
        ///     reputation score
        /// </summary>
        public int ReputationScore { get; set; } = 0;

        public ICollection<Offering>? Offerings { get; set; } = [];

        public ICollection<Need>? Needs { get; set; } = [];
    }

    /// <summary>
    ///     verification status
    /// </summary>
    public enum VerificationStatus
    {
        /// <summary>
        ///     registered
        /// </summary>
        Unverified,
        /// <summary>
        ///     pending - requires more info
        /// </summary>
        Pending,
        /// <summary>
        ///     verified details
        /// </summary>
        Verified,
        /// <summary>
        ///     rejected - verified user is not a genuine user who has needs/offerings
        /// </summary>
        Rejected,
        /// <summary>
        ///     removed - user requested account to be removed
        /// </summary>
        Removed
    }
}
