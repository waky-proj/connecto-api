using Connecto.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Connecto.Infrastructure.Services.Data.Seed
{
    /// <summary>
    /// Seeds the initial sample data used by the application during database setup.
    /// </summary>
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
                new () { Id = Guid.Parse("c4d3ee71-e814-407f-a7bc-be260734880f"), FirstName = "Alice", MiddleName = "Marie", LastName = "Johnson", City = "Auckland", Address = "123 Main St", Bio = string.Empty, Country = "New Zealand", Email = "alice.johnson@example.com" },
                new () { Id = Guid.Parse("9d5377df-7f3b-4bce-84e4-0dc88984c35d"), FirstName = "Bob", MiddleName = "James", LastName = "Smith", City = "Wellington", Address = "456 Oak Ave", Bio = string.Empty, Country = "New Zealand", Email = "bob.smith@example.com" }
            };
            modelBuilder.Entity<User>().HasData(users);

            var needs = new List<Need> {
                new () { Id = Guid.Parse("99bbb451-d3bb-4615-a371-688f1e82e0e8"), UserId = users[0].Id, Title = "Need a web developer", Description = "Need a web developer", Location = "Auckland", Category = "Technical" },
                new () { Id = Guid.Parse("b5b8ec4d-f075-44d0-a431-c116504798c3"), UserId = users[1].Id, Title = "Need a graphic designer", Description = "Need a graphic designer", Location = "Wellington", Category = "Creative" }
            };
            modelBuilder.Entity<Need>().HasData(needs);

            var needSkills = new List<NeedSkill> {
                new () { Id = Guid.Parse("63868ebc-8600-4c11-bdbc-025ee539dd5f"), NeedId = needs[0].Id, Skill = Skill.Design },
                new () { Id = Guid.Parse("60aac080-5a39-4ac0-b517-84f08c4375cf"), NeedId = needs[1].Id, Skill = Skill.Programming }
            };
            modelBuilder.Entity<NeedSkill>().HasData(needSkills);
        }

        public static void SeedOfferings(ModelBuilder modelBuilder)
        {
            var users = new List<User> {
                new () { Id = Guid.Parse("25073136-a13e-4b78-881f-138062c9fb44"), FirstName = "John", MiddleName = "Dion", LastName = "Doe", City = "Auckland", Address = "789 Pine Rd", Bio = string.Empty, Country = "New Zealand", Email = "john.doe@example.com" },
                new () { Id = Guid.Parse("2ecd4582-6582-420a-8edc-96be5753de13"), FirstName = "Amanda", MiddleName = "Arion", LastName = "Lee", City = "Wellington", Address = "321 Elm St", Bio = string.Empty, Country = "New Zealand", Email = "amanda.lee@example.com" }
            };
            modelBuilder.Entity<User>().HasData(users);

            var offerings = new List<Offering>
            {
                new () { Id = Guid.Parse("35e3b13f-4b46-44d1-a5aa-0b3e45c8741b"), UserId = users[0].Id, Title = "Offering web development services", Description = "Offering web development services", Location = "Auckland", Category = "Technical" },
                new () { Id = Guid.Parse("fae444c5-45d5-4375-befd-90eb566bfb23"), UserId = users[1].Id, Title = "Offering graphic design services", Description = "Offering graphic design services", Location = "Wellington", Category = "Creative" }
            };
            modelBuilder.Entity<Offering>().HasData(offerings);

            var offeringSkills = new List<OfferingSkill>
            {
                new () { Id = Guid.Parse("07518520-cf01-4ca4-bd28-ecd69d9abbee"), OfferingId = offerings[0].Id, Skill = Skill.Design },
                new () { Id = Guid.Parse("374259b5-16e5-4bd5-b1cf-5680c6a117d2"), OfferingId = offerings[1].Id, Skill = Skill.Programming }
            };
            modelBuilder.Entity<OfferingSkill>().HasData(offeringSkills);
        }
    }
}

