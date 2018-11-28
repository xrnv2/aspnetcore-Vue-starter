using System.Linq;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using Vue2Spa.Providers;
using Vue2Spa.SQLite;
using System;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.Extensions.Options;
using Vue2Spa.Services;
using Vue2Spa.Helpers;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace Vue2Spa.Controllers
{
    [Route("api/[controller]")]
    public class AuthController : Controller
    {
        private readonly IWeatherProvider weatherProvider;
        private readonly sqliteContext _context;
        private IUserService _userService;
        private readonly AppSettings _appSettings;

        public AuthController(sqliteContext context, IWeatherProvider weatherProvider, IUserService userService
            /* IMapper mapper*/,
            IOptions<AppSettings> appSettings )
        {
            this.weatherProvider = weatherProvider;
            _context = context;
            _userService = userService;
            _appSettings = appSettings.Value;

        }

        private object HashPassword(string password){

            // generate a 128-bit salt using a secure PRNG
            byte[] salt = new byte[128 / 8];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
            }
            Console.WriteLine($"Salt: {Convert.ToBase64String(salt)}");

            // derive a 256-bit subkey (use HMACSHA1 with 10,000 iterations)
            string hashed = Convert.ToBase64String(KeyDerivation.Pbkdf2(
                password: password,
                salt: salt,
                prf: KeyDerivationPrf.HMACSHA1,
                iterationCount: 10000,
                numBytesRequested: 256 / 8));
            Console.WriteLine($"Hashed: {hashed}");
            return new {hashed, salt};
        }

        [HttpPost("login")]
        public IActionResult Forecasts(User idata)
        {
            try{
                var row = _context.Users.FirstOrDefault(e=> e.Username == idata.Username);
                if(row == null){
                    return Unauthorized();
                }else{
                    var hp = HashPassword(idata.Password);
                    if(  row.Password != idata.Password){
                        return Unauthorized();
                    }else{
                        var tokenHandler = new JwtSecurityTokenHandler();
                        var key = Encoding.ASCII.GetBytes(_appSettings.Secret);
                        var tokenDescriptor = new SecurityTokenDescriptor
                        {
                            Subject = new ClaimsIdentity(new Claim[]
                            {
                                new Claim(ClaimTypes.Name, row.Id.ToString())
                            }),
                            Expires = DateTime.UtcNow.AddDays(7),
                            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
                        };
                        var token = tokenHandler.CreateToken(tokenDescriptor);
                        var tokenString = tokenHandler.WriteToken(token);
                        row.Token = tokenString;
                        _context.SaveChangesAsync();
                        return Ok(new {row.FirstName, row.LastName, row.Username, row.Token} );
                    }
                }
            }
            catch(Exception e)   {
                return Unauthorized();
            }
        }

        [HttpPost("register")]
        public IActionResult Register (User idata)
        {
            //_context.RemoveRange(_context.Users.Where(e=> e.Id < 5));
            try{
                idata.PasswordHash = HashPassword(idata.Password);
                _context.AddAsync(idata);
                _context.SaveChangesAsync();
                var list = _context.Users.ToList();
                Console.WriteLine("select"+ list);
                return Ok();
            }
            catch(Exception e)   {
                return BadRequest();
            }

        }
        public class UserViewModel{
            string login {get; set;}
            string password {get; set;}

        }
    }
}
