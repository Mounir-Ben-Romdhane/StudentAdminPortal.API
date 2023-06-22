﻿using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using StudentAdminPortal.API.DataModels;
using StudentAdminPortal.API.DomainModels;
using StudentAdminPortal.API.DomainModels.Dto;
using StudentAdminPortal.API.Helpers;
using StudentAdminPortal.API.UtilityService;
using System.Collections;
using System.Collections.Generic;
using System.Data.Entity.Core.Common.CommandTrees.ExpressionBuilder;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace StudentAdminPortal.API.Repositories
{
    public class UserRepository : IUserRepository
    {

        private readonly StudentAdminContext context;
        private readonly IConfiguration _configuration;
        private readonly IEmailService _emailService;
        public UserRepository(StudentAdminContext context
            , IConfiguration configuration,
            IEmailService emailService)
        {
            this.context = context;
            this._configuration = configuration;
            this._emailService = emailService;
        }


        public async Task<List<User>> GetUsersAsync()
        {
            return await context.Users.Include(nameof(Role)).ToListAsync();
        }

        public async Task<User> Authenticate(string username)
        {
            return await context.Users.FirstOrDefaultAsync(x => x.Username == username || x.Email == username);
        }


        public async Task<User>  RegisterUser(User request)
        {
            request.PasswordHashed = PasswordHacher.HashPassword(request.Password);
            request.RoleId = Guid.Parse("6AB6245F-68DF-4927-9FE4-E9FFCED547D0");
            request.Token = "";
            var user = await context.Users.AddAsync(request);
            await context.SaveChangesAsync();
            return user.Entity;
        }
            

        public Task<bool> CheckUserNameExistAsync(string username)
        => context.Users.AnyAsync(x => x.Username == username);

        public Task<bool> CheckEmailExistAsync(string email)
            => context.Users.AnyAsync(x => x.Email == email);

        public string CheckPasswordStrengthAsync(string password)
        {
            StringBuilder sb = new StringBuilder();
            if(password.Length < 8)
                sb.Append("Password must be at least 8 characters long &&"+Environment.NewLine);
            if(!(Regex.IsMatch(password,"[a-z]") && Regex.IsMatch(password,"[A-Z]") && 
                Regex.IsMatch(password,"[0-9]")))
                sb.Append(" Password must contain at least one uppercase letter, one lowercase letter and one number &&"+Environment.NewLine);
            if(!Regex.IsMatch(password,"[!@#$%^&*()_+=\\[\\]{};':\"\\\\|,.<>\\/?]"))
                sb.Append(" Password must contain at least one special character"+Environment.NewLine);  
            return sb.ToString();
        }

        public string CreateJwtToken(User user)
        {
            
            
            List<string> claimsNames = new List<string>();
            claimsNames = context.Claims.Where(x => x.RoleId == user.RoleId).
            Select(x => x.claimName).
            ToList();
            
            string jsonList = JsonConvert.SerializeObject(claimsNames);

            var role = context.Roles.FirstOrDefault(x => x.Id == user.RoleId);
            var jwtTokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes("veryverysecret.....");
            var identity = new ClaimsIdentity(new Claim[]
            {
                new Claim(ClaimTypes.Role,role.RoleName),
                new Claim(ClaimTypes.Name,$"{user.Username}"),
                new Claim("myClaims",jsonList)
            });
            var credentials = new SigningCredentials(new SymmetricSecurityKey(key),SecurityAlgorithms.HmacSha256);
            
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = identity,
                Expires = DateTime.Now.AddSeconds(10),
                SigningCredentials = credentials
            };
            var token = jwtTokenHandler.CreateToken(tokenDescriptor);
            return jwtTokenHandler.WriteToken(token);
        }

        public string CreateRefreshToken()
        {
            var tokenBytes = RandomNumberGenerator.GetBytes(64);
            var refreshToken = Convert.ToBase64String(tokenBytes); 

            var tokenInUser = context.Users.
                Any(x => x.RefreshToken == refreshToken);

            if(tokenInUser)
            {
                return CreateRefreshToken();
            }
            return refreshToken;
        }

        public ClaimsPrincipal GetPrincipaleFromExpireToken(string token)
        {
            var tokenValidationParameters = new TokenValidationParameters
            {
                ValidateAudience = false,
                ValidateIssuer = false,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes("veryverysecret.....")),
                ValidateIssuerSigningKey = true,
                ValidateLifetime = false
            };
            var tokenHandler = new JwtSecurityTokenHandler();
            SecurityToken securityToken;
            var principal = tokenHandler.ValidateToken(token,tokenValidationParameters,out securityToken);
            var jwtSecuriteToken = securityToken as JwtSecurityToken;
            if(jwtSecuriteToken == null || !jwtSecuriteToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256,StringComparison.InvariantCultureIgnoreCase))
            {
                throw new SecurityTokenException("Invalid Token");
            }
            return principal;
        }

        public async Task<User> RefreshToken(TokenApiDto tokenApiDto)
        {
            string accessToken = tokenApiDto.AccessToken;
            string refreshToken = tokenApiDto.RefreshToken;
            var principal = GetPrincipaleFromExpireToken(accessToken);
            var username = principal.Identity.Name;
            var user = await context.Users.FirstOrDefaultAsync(x => x.Username == username);
            return user;
        }

        public async Task<List<Role>> GetAllRoles()
        {
            return await context.Roles.Include(r => r.claims).ToListAsync();
        }

        public async Task<string> GetAllCLaims(Guid roleId)
        {
            List<string> list = new List<string>();
            list = context.Claims.Where(x => x.RoleId == roleId).
            Select(x => x.claimName).
            ToList();
            string jsonList = JsonConvert.SerializeObject(list);
            return jsonList;
        }

        public async Task<User> GetUserAsync(int userId)
        {
           return await context.Users
                .FirstOrDefaultAsync(u => u.Id == userId);
        }

        public async Task<User> UpdateUserAsync(int userId, UpdateUserRequest request)
        {
            var existingUser = await GetUserAsync(userId);
            if(existingUser != null)
            {
                existingUser.FirstName = request.FirstName;
                existingUser.LastName = request.LastName;
                existingUser.Username = request.Username;
                existingUser.Email = request.Email;
                existingUser.Password = existingUser.Password;
                existingUser.PasswordHashed = existingUser.PasswordHashed;
                existingUser.RoleId = request.RoleId;
                existingUser.Token = existingUser.Token;
                existingUser.RefreshToken = existingUser.RefreshToken;
                existingUser.RefreshTOkenExpiryTime = existingUser.RefreshTOkenExpiryTime;
                
                await context.SaveChangesAsync();
                return existingUser;
            }
            return null;
        }

        public async Task<User> DeleteUserAsync(int userId)
        {
            var user = await GetUserAsync(userId);
            if(user != null)
            {
                context.Users.Remove(user);
                await context.SaveChangesAsync();
                return user;
            }
            return null;
        }

        public async Task<User> AddUserAsync(User request)
        {
            request.PasswordHashed = PasswordHacher.HashPassword(request.Password);
            request.RoleId = request.RoleId;
            request.Token = "";
            var user = await context.Users.AddAsync(request);
            await context.SaveChangesAsync();
            return user.Entity;
        }

        public async Task<bool> ExistUser(int userId)
        {
            return await context.Users.AnyAsync(u => u.Id == userId);
        }

        public async Task<User> GetUserByEmail(string email)
        {
            return await context.Users.FirstOrDefaultAsync(u => u.Email == email);
        }

        public async Task SendEmail( User user)
        {
            

            
            context.Entry(user).State = EntityState.Modified;
            await context.SaveChangesAsync();
        }

        public async Task<User> GetUserByEmailToken(string emailToken)
        {
            return await context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Email == emailToken);
        }

        public async Task SendResetEmail(User user)
        {
            context.Entry(user).State = EntityState.Modified;
            await context.SaveChangesAsync();
        }
    }
}
