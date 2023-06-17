﻿using AutoMapper;
using Azure.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudentAdminPortal.API.DataModels;
using StudentAdminPortal.API.DomainModels;
using StudentAdminPortal.API.DomainModels.Dto;
using StudentAdminPortal.API.Helpers;

namespace StudentAdminPortal.API.Controllers
{

    [Route("[controller]")]
    [ApiController]
    public class UserController : ControllerBase
    {
        private readonly IUnitOfWork uow;

        public UserController(IUnitOfWork uow)
        {
            this.uow = uow;
        }

        [HttpGet]
        [Authorize]
        [Route("/Users")]
        public async Task<IActionResult> GetAllUsers()
        {
            var users = await uow.UserRepository.GetUsersAsync();

            return Ok(users);
        }

        [HttpGet]
        //[Authorize]
        [Route("/Claims")]
        public async Task<IActionResult> GetAllClaims(Guid roleId)
        {
            var claims = await uow.UserRepository.GetAllCLaims(roleId);

            if (claims == null)
            {
                return NotFound();
            }

            return Ok(claims);
        }



        [HttpGet]
        [Route("/Roles")]
        public async Task<IActionResult> GetAllRoles()
        {
            var roles = await uow.UserRepository.GetAllRoles();

            return Ok(roles);
        }


        [HttpPost("authenticate")]
        public async Task<IActionResult> Authenticate([FromBody] User userObj)
        {
            if (userObj == null)
            {
                return NotFound();
            }

            var user = await uow.UserRepository.Authenticate(userObj.Username);

            if (user == null)
                return NotFound(new {Message = "User not found !"});

            if (!PasswordHacher.VerifyPassword(userObj.Password, user.PasswordHashed)){
                return BadRequest(new { Message = "Password is incorrect !" });
            }

            
             user.Token = uow.UserRepository.CreateJwtToken(user);
            var newAccessToken = user.Token;
            var newRefreshToken = uow.UserRepository.CreateRefreshToken();
            user.RefreshToken = newRefreshToken;
            user.RefreshTOkenExpiryTime =DateTime.Now.AddDays(5);
            uow.Complete();
             

            return Ok(new TokenApiDto()
            {
                AccessToken = newAccessToken,
                RefreshToken = newRefreshToken
            });
        }

        [HttpPost("register")]
        public async Task<IActionResult> RegisterUser([FromBody] User request)
        {
            if (request == null)
            {
                return NotFound();
            }

            var pass = uow.UserRepository.CheckPasswordStrengthAsync(request.Password);

            if (await uow.UserRepository.CheckUserNameExistAsync(request.Username))
            {
                return BadRequest("UserName already exists");
            }else if (await uow.UserRepository.CheckEmailExistAsync(request.Email))
            {
                return BadRequest("Email already exists");
            }else if(!string.IsNullOrEmpty(pass))
            {
                return BadRequest(pass);
            }
            else
            {
                await uow.UserRepository.RegisterUser(request);

                return Ok(new { Message = "User registered successfully !!" });
            }
        }

        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh(TokenApiDto tokenApiDto)
        {
            if(tokenApiDto == null)
            {
                return BadRequest("Invalid client request");
            }
            var user = await uow.UserRepository.RefreshToken(tokenApiDto);
            if (user is null || user.RefreshToken != tokenApiDto.RefreshToken || user.RefreshTOkenExpiryTime <= DateTime.Now)
            {
                return BadRequest("Invalid Request");
            }
            var newAccessToken = uow.UserRepository.CreateJwtToken(user);
            var newRefreshToken = uow.UserRepository.CreateRefreshToken();
            user.RefreshToken = newRefreshToken;
            uow.Complete();
            return Ok(new TokenApiDto()
            {
                AccessToken = newAccessToken,
                RefreshToken = newRefreshToken
            });

        }

        [HttpGet]
        [Route("/Users/{userId:int}")]
        public async Task<IActionResult> GetUser(int userId)
        {
            var user = await uow.UserRepository.GetUserAsync(userId);
            var claims = await uow.UserRepository.GetAllCLaims(user.RoleId);

            if (user == null)
            {
                return NotFound();
            }

            var userDto = new UserDto()
            {
                Id = user.Id,
                UserName = user.Username,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                RoleId = user.RoleId,
                Password = user.Password
            };

            return Ok(userDto);
        }

        [HttpPut]
        [Route("/Users/{userId:int}")]
        public async Task<IActionResult> UpdateUser([FromRoute] int userId,
            [FromBody] UpdateUserRequest request)
        {
            //Check user exist
            if(await uow.UserRepository.ExistUser(userId)){
                //Update user
                var updateUser = await uow.UserRepository.UpdateUserAsync(userId,request);
                if(updateUser != null)
                {
                    return Ok(updateUser);
                }
            }
            return NotFound();
        }

        [HttpPost]
        [Route("/Users/Add")]
        public async Task<IActionResult> AddUser([FromBody] User request)
        {
            if(request == null)
            {
                return NotFound();
            }            
            var pass = uow.UserRepository.CheckPasswordStrengthAsync(request.Password);
            if (await uow.UserRepository.CheckUserNameExistAsync(request.Username))
            {
                return BadRequest("UserName already exists");
            }
            else if (await uow.UserRepository.CheckEmailExistAsync(request.Email))
            {
                return BadRequest("Email already exists");
            }
            else if (!string.IsNullOrEmpty(pass))
            {
                return BadRequest(pass);
            }
            else
            {
                await uow.UserRepository.AddUserAsync(request);

                return Ok(new { Message = "User added successfully !!" });
            }
        }

        [HttpDelete]
        [Route("/Users/{userId:int}")]
        public async Task<IActionResult> DeleteUser([FromRoute] int userId)
        {
            //Check user exist
            if (await uow.UserRepository.ExistUser(userId))
            {
                //Delete user
                var deleteUser = await uow.UserRepository.DeleteUserAsync(userId);
                if (deleteUser != null)
                {
                    return Ok(deleteUser);
                }
            }
            return NotFound();
        }
    }
}
