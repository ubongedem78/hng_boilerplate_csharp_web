﻿using CSharpFunctionalExtensions;
using Hng.Application.Features.UserManagement.Dtos;
using Hng.Domain.Entities;
using Hng.Infrastructure.Repository.Interface;
using Hng.Infrastructure.Services.Interfaces;
using Hng.Infrastructure.Utilities;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using System.Text;

namespace Hng.Application.Features.UserManagement.Handlers
{
    public class ForgotPasswordHandler : IRequestHandler<ForgotPasswordDto, Result<ForgotPasswordResponse>>
    {
        private readonly IRepository<User> _userRepo;
        private readonly IMessageQueueService _queueService;
        private readonly IOptions<FrontendUrl> _options;

        public ForgotPasswordHandler(
            IRepository<User> userRepo,
            IMessageQueueService queueService,
            IOptions<FrontendUrl> options)
        {
            _userRepo = userRepo;
            _queueService = queueService;
            _options = options;
        }

        public async Task<Result<ForgotPasswordResponse>> Handle(ForgotPasswordDto request, CancellationToken cancellationToken)
        {
            string code = null!;
            var user = await _userRepo.GetBySpec(u => u.Email == request.Email);

            if (user == null)
                return Result.Failure<ForgotPasswordResponse>("User with email does not exist");

            if (!request.IsMobile)
            {
                code = Guid.NewGuid().ToString().Replace("-", "");
                var encodedUserId = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(user.Id.ToString()));
                var pageLink = $"{_options.Value.Path}/reset-password/{encodedUserId}/{code}";

                //send email
                await _queueService.SendForgotPasswordEmailAsync(
                    user.FirstName ?? user.LastName,
                    user.Email,
                    "Telex BiolerPlate",
                    pageLink,
                    DateTime.UtcNow.Year.ToString());
            }
            else
            {
                code = GenerateSixDigitCode();

                //send email
                await _queueService.SendForgotPasswordEmailMobileAsync(
                    user.FirstName ?? user.LastName,
                    user.Email,
                    "Telex BiolerPlate",
                    code,
                    DateTime.UtcNow.Year.ToString());
            }

            user.PasswordResetToken = code;
            user.PasswordResetTokenTime = DateTime.UtcNow;

            await _userRepo.UpdateAsync(user);
            await _userRepo.SaveChanges();

            return Result.Success(new ForgotPasswordResponse()
            {
                Message = "successful",
                StatusCode = StatusCodes.Status200OK,
                Data = new ForgotPasswordData()
                {
                    Message = "A mail has been sent to your email address"
                }
            });
        }

        public static string GenerateSixDigitCode()
        {
            Random random = new Random();
            int number = random.Next(100000, 999999);
            return number.ToString("D6");
        }
    }
}