using Microsoft.AspNetCore.Mvc;
using WebApi.MinimalApi.Domain;
using WebApi.MinimalApi.Models;
using AutoMapper;

namespace WebApi.MinimalApi.Controllers;

[Route("api/[controller]")]
[ApiController]
[Produces("application/json", "application/xml")]
public class UsersController : Controller
{
    private readonly IUserRepository _userRepository;
    private readonly IMapper _mapper;
    // Чтобы ASP.NET положил что-то в userRepository требуется конфигурация
    public UsersController(IUserRepository userRepository, IMapper mapper)
    {
        _userRepository = userRepository;
        _mapper = mapper;
    }

    [HttpGet("{userId}")]
    public ActionResult<UserDto> GetUserById([FromRoute] Guid userId)
    {
        // поиск пользователя в репозитории
        var userEntity = _userRepository.FindById(userId);
        
        // не нащел - 404
        if (userEntity == null)
        {
            return NotFound();
        }

        // Маппим Entity в DTO 
        var userDto = new UserDto
        {
            Id = userEntity.Id,
            Login = userEntity.Login,
            FullName = $"{userEntity.LastName} {userEntity.FirstName}"
        };

        // возвращаем 200 и объект
        return Ok(userDto);
    }

    [HttpPost]
    public IActionResult CreateUser([FromBody] object user)
    {
        throw new NotImplementedException();
    }
}

