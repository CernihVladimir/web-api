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
    [HttpHead("{userId}")]
    public ActionResult<UserDto> GetUserById([FromRoute] Guid userId)
    {
        // поиск пользователя в репозитории (проверьте, GetById или FindById требует ваш репозиторий)
        var userEntity = _userRepository.FindById(userId);
        
        // не нашёл - 404
        if (userEntity == null)
        {
            return NotFound();
        }

        // Если это HEAD запрос, возвращаем только заголовки (без тела)
        if (HttpMethods.IsHead(Request.Method))
        {
            return Ok();
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
    public IActionResult CreateUser([FromBody] CreateUserDto user)
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

    [HttpPut("{userId}")]
    public IActionResult UpdateUser(
        [FromRoute] Guid userId,
        [FromBody] UpdateDto user)
    {
        if (!Guid.TryParse(
            RouteData.Values["userId"]?.ToString(),
            out var parsedUserId))
        {
            return BadRequest();
        }

        if (user == null)
        {
            return BadRequest();
        }

        if (!ModelState.IsValid)
        {
            return UnprocessableEntity(ModelState);
        }

        var userEntity = _userRepository.FindById(parsedUserId)
            ?? new UserEntity(parsedUserId);

        _mapper.Map(user, userEntity);

        _userRepository.UpdateOrInsert(userEntity, out var isInserted);

        if (isInserted)
        {
            return CreatedAtRoute(
                nameof(GetUserById),
                new { userId = parsedUserId },
                parsedUserId.ToString());
        }

        return NoContent();
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

        // Маппим обновленный DTV обратно в Entity и сохраняем в репозиторий
        _mapper.Map(updateDto, userEntity);
        _userRepository.Update(userEntity);

        return NoContent();
    }

    [HttpDelete("{userId}")]
    public IActionResult DeleteUser([FromRoute] Guid userId)
    {
        var user = _userRepository.FindById(userId);
        if (user == null)
        {
            return NotFound();
        }

        _userRepository.Delete(userId);
        return NoContent();
    }

    [HttpOptions]
    public IActionResult GetUsersOptions()
    {
        Response.Headers["Allow"] = "GET, POST, OPTIONS";
        return Ok();
    }
}