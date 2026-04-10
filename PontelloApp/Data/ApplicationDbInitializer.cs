using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PontelloApp.Models;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace PontelloApp.Data
{
    public static class ApplicationDbInitializer
    {
        public static async Task Initialize(IServiceProvider serviceProvider,
            bool useMigrations = true, bool seedSampleData = true)
        {
            #region Prepare the Database
            if (useMigrations)
            {
                var context = serviceProvider.GetRequiredService<ApplicationDbContext>();
                try
                {
                    await context.Database.MigrateAsync();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Migration error: " + ex.GetBaseException().Message);
                }
            }
            #endregion

            #region Seed Roles and Users
            if (seedSampleData)
            {
                var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
                var userManager = serviceProvider.GetRequiredService<UserManager<User>>();

                string[] roles = { "Admin", "Dealer" };

                // Create roles if not exist
                foreach (var role in roles)
                {
                    if (!await roleManager.RoleExistsAsync(role))
                    {
                        await roleManager.CreateAsync(new IdentityRole(role));
                    }
                }

                // Helper: create user if not exists
                async Task CreateUserIfNotExists(string email, string password, string role)
                {
                    var existingUser = await userManager.FindByEmailAsync(email);
                    if (existingUser == null)
                    {
                        var user = new User
                        {
                            UserName = email,
                            Email = email,
                            PhoneNumber = "9057362876",
                            EmailConfirmed = true,
                            FirstName = email.Split('@')[0],
                            LastName = "Pontello",
                            BINorEIN = null,
                            Status= AccountStatus.Active
                        };

                        var result = await userManager.CreateAsync(user, password);
                        if (result.Succeeded)
                        {
                            await userManager.AddToRoleAsync(user, role);
                        }
                        else
                        {
                            foreach (var error in result.Errors)
                            {
                                Debug.WriteLine($"Error creating user {email}: {error.Description}");
                            }
                        }
                    }
                }

                string defaultPassword = "@Pontello123";

                await CreateUserIfNotExists("admin@gmail.com", defaultPassword, "Admin");
                await CreateUserIfNotExists("dealer@gmail.com", defaultPassword, "Dealer");
            }
            #endregion
        }
    }
}
