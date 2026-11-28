using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ResourceBookingSystem.WebApp.Auth.DTOs;
using ResourceBookingSystem.WebApp.Auth.ServiceInterfaces;

namespace ResourceBookingSystem.API.Controllers
{
    [Route("api/auth")]
    [ApiController]
    public class AuthController(IAuthService authService) : ControllerBase
    {
        [HttpPost("register")]
        public async Task<IActionResult> Register(RegisterRequestDto registerRequestDto)
        {
            var result = await authService.Register(registerRequestDto);
            if (result is null)
            {
                return Problem(
                    statusCode: StatusCodes.Status409Conflict,
                    title: "Already Existed",
                    detail: "User with this email already exists.");
            }
            return Ok(result);
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginRequestDto loginRequestDto)
        {
            var result = await authService.Login(loginRequestDto);
            if (result is null)
            {
                return Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Invalid email or password",
                    detail: "Invalid email or password");
            }

            AddRefreshTokenCookie(result.RefreshToken);

            return Ok(new LoginResponseDto
            {
                User = result.User,
                AccessToken = result.AccessToken
            });
        }

        [HttpPost("refresh-token")]
        public async Task<IActionResult> RefreshToken()
        {
            var refreshToken = Request.Cookies["refreshToken"];
            if (string.IsNullOrEmpty(refreshToken))
            {
                DeleteRefreshTokenCookie();
                return Unauthorized(); 
            }

            var result = await authService.Refresh(refreshToken);
            if (result is null)
            {
                DeleteRefreshTokenCookie(); 
                return Unauthorized();   
            }

            AddRefreshTokenCookie(result.RefreshToken);

            return Ok(new RefreshTokenResponseDto
            {
                User = result.User,
                AccessToken = result.AccessToken
            });
        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            var refreshToken = Request.Cookies["refreshToken"];
            if (!string.IsNullOrEmpty(refreshToken))
            {
                await authService.RevokeRefreshToken(refreshToken);
            }

            DeleteRefreshTokenCookie();

            return NoContent();
        }

        private void AddRefreshTokenCookie(string refreshToken)
        {
            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.None,
                Expires = DateTimeOffset.UtcNow.AddDays(7),
                Path = "/"
            };

            Response.Cookies.Append("refreshToken", refreshToken, cookieOptions);
        }

        private void DeleteRefreshTokenCookie()
        {
            Response.Cookies.Delete("refreshToken", new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.None,
                Path = "/"
            });
        }
    }
}