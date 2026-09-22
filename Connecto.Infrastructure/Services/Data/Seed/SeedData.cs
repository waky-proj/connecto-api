using Connecto.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Connecto.Infrastructure.Services.Data.Seed
{
    public static class SeedData
    {
        public static void Seed(ModelBuilder modelBuilder)
        {
            SeedNeeds(modelBuilder);
            SeedOfferings(modelBuilder);
        }

        private static void SeedNeeds(ModelBuilder modelBuilder)
        {
            var users = new List<User> {
                new () { FirstName = "Alice", MiddleName = "Marie", LastName = "Johnson", City = "Auckland", Address = "123 Main St", Bio = string.Empty, Country = "New Zealand", Email = "alice.johnson@example.com" },
                new () { FirstName = "Bob", MiddleName = "James", LastName = "Smith", City = "Wellington", Address = "456 Oak Ave", Bio = string.Empty, Country = "New Zealand", Email = "bob.smith@example.com" }
            };
            modelBuilder.Entity<User>().HasData(users);

            var needs = new List<Need> {
                new () { UserId = users[0].Id, Title = "Need a web developer", Description = "Need a web developer", Location = "Auckland", Category = "Technical" },
                new () { UserId = users[1].Id, Title = "Need a graphic designer", Description = "Need a graphic designer", Location = "Wellington", Category = "Creative" }
            };
            modelBuilder.Entity<Need>().HasData(needs);

            var needSkills = new List<NeedSkill> {
                new () { NeedId = needs[0].Id, Skill = Skill.Design },
                new () { NeedId = needs[1].Id, Skill = Skill.Programming }
            };
            modelBuilder.Entity<NeedSkill>().HasData(needSkills);
        }

        public static void SeedOfferings(ModelBuilder modelBuilder)
        {
            var users = new List<User> {
                new () { FirstName = "John", MiddleName = "Dion", LastName = "Doe", City = "Auckland", Address = "789 Pine Rd", Bio = string.Empty, Country = "New Zealand", Email = "john.doe@example.com" },
                new () { FirstName = "Amanda", MiddleName = "Arion", LastName = "Lee", City = "Wellington", Address = "321 Elm St", Bio = string.Empty, Country = "New Zealand", Email = "amanda.lee@example.com" }
            };
            modelBuilder.Entity<User>().HasData(users);

            var offerings = new List<Offering>
            {
                new () { UserId = users[0].Id, Title = "Offering web development services", Description = "Offering web development services", Location = "Auckland", Category = "Technical" },
                new () { UserId = users[1].Id, Title = "Offering graphic design services", Description = "Offering graphic design services", Location = "Wellington", Category = "Creative" }
            };
            modelBuilder.Entity<Offering>().HasData(offerings);

            var offeringSkills = new List<OfferingSkill>
            {
                new () { OfferingId = offerings[0].Id, Skill = Skill.Design },
                new () { OfferingId = offerings[1].Id, Skill = Skill.Programming }
            };
            modelBuilder.Entity<OfferingSkill>().HasData(offeringSkills);
        }
    }
}
