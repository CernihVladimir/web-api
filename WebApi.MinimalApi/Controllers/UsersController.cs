using Microsoft.AspNetCore.Mvc;
using WebApi.MinimalApi.Domain;
using WebApi.MinimalApi.Models;
using Microsoft.AspNetCore.JsonPatch;
using AutoMapper;

namespace WebApi.MinimalApi.Controllers;

[Route("api/[controller]")]
[ApiController]
[Produces("application/json", "application/xml")]
public class UsersController : Controller
{
    private readonly IUserRepository _userRepository;
    private readonly IMapper _mapper;
    public UsersController(IUserRepository userRepository, IMapper mapper)
    {
        _userRepository = userRepository;
        _mapper = mapper;
    }

    [HttpGet("{userId}", Name = nameof(GetUserById))]
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
    public IActionResult CreateUser([FromBody] CreateUserDto user) // fffffffffffffffffffffff
    {
        if (user == null || !ModelState.IsValid)
        {
            return UnprocessableEntity(ModelState);
        }

        if (!user.Login.All(char.IsLetterOrDigit))
        {
            ModelState.AddModelError("Login", "Login should contain only letters or digits");
            return UnprocessableEntity(ModelState);
        }

        var userEntity = _mapper.Map<UserEntity>(user);
        _userRepository.Insert(userEntity);

        var createdDto = _mapper.Map<UserDto>(userEntity);

        return CreatedAtRoute(
            nameof(GetUserById),
            new { userId = userEntity.Id },
            createdDto
        );
    }

    [HttpPatch("{userId}")]
    public IActionResult PartiallyUpdateUser([FromRoute] Guid userId, [FromBody] JsonPatchDocument<UpdateDto> patchDoc)
    {
        if (patchDoc == null)
        {
            return BadRequest();
        }

        var userEntity = _userRepository.FindById(userId);
        if (userEntity == null)
        {
            return NotFound();
        }

        // Маппим сущность в UpdateDto для применения патча
        var updateDto = _mapper.Map<UpdateDto>(userEntity);

        // Применяем изменения из патча к DTO
        patchDoc.ApplyTo(updateDto, ModelState);

        // Валидируем модель после применения патча
        if (!TryValidateModel(updateDto))
        {
            return UnprocessableEntity(ModelState);
        }

        // Маппим обновленный DTO обратно в Entity и сохраняем в репозиторий
        _mapper.Map(updateDto, userEntity);
        _userRepository.Update(userEntity);

        return NoContent();
    }
}