using Microsoft.AspNetCore.Mvc;
using WebApi.MinimalApi.Domain;
using WebApi.MinimalApi.Models;
using Microsoft.AspNetCore.JsonPatch;
using AutoMapper;
using Microsoft.AspNetCore.Routing;
using Newtonsoft.Json;

namespace WebApi.MinimalApi.Controllers;

[Route("api/[controller]")]
[ApiController]
[Produces("application/json", "application/xml")]
public class UsersController : Controller
{
    private readonly LinkGenerator _linkGenerator;
    private readonly IUserRepository _userRepository;
    private readonly IMapper _mapper;

    public UsersController(
        IUserRepository userRepository,
        IMapper mapper,
        LinkGenerator linkGenerator)
    {
        _userRepository = userRepository;
        _mapper = mapper;
        _linkGenerator = linkGenerator;
    }

    [HttpGet(Name = nameof(GetUsers))]
    public IActionResult GetUsers(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10)
    {
        // Ограничиваем параметры допустимыми значениями.
        pageNumber = Math.Max(1, pageNumber);
        pageSize = Math.Clamp(pageSize, 1, 20);

        // Получаем страницу пользователей.
        var pageList = _userRepository.GetPage(pageNumber, pageSize);

        // Преобразуем сущности в DTO.
        var users = _mapper.Map<IEnumerable<UserDto>>(pageList);

        // Формируем ссылки на соседние страницы.
        var previousPageLink = pageNumber > 1
            ? _linkGenerator.GetUriByRouteValues(
                HttpContext,
                nameof(GetUsers),
                new
                {
                    pageNumber = pageNumber - 1,
                    pageSize
                })
            : null;

        var nextPageLink = pageNumber < pageList.TotalPages
            ? _linkGenerator.GetUriByRouteValues(
                HttpContext,
                nameof(GetUsers),
                new
                {
                    pageNumber = pageNumber + 1,
                    pageSize
                })
            : null;

        // Формируем информацию о пагинации.
        var paginationHeader = new
        {
            previousPageLink,
            nextPageLink,
            totalCount = pageList.TotalCount,
            pageSize = pageList.PageSize,
            currentPage = pageList.CurrentPage,
            totalPages = pageList.TotalPages
        };

        Response.Headers["X-Pagination"] =
            JsonConvert.SerializeObject(paginationHeader);

        return Ok(users);
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
        if (user == null)
        {
            return BadRequest();
        }

        if (!ModelState.IsValid)
        {
            var errors = ModelState
                .Where(entry => entry.Value?.Errors.Count > 0)
                .ToDictionary(
                    entry => entry.Key.ToLowerInvariant(),
                    entry => entry.Value!.Errors
                        .Select(error => error.ErrorMessage)
                        .ToArray()
                );

            return new ObjectResult(errors)
            {
                StatusCode = 422,
                ContentTypes = { "application/json" }
            };
        }

        if (!user.Login.All(char.IsLetterOrDigit))
        {
            var errors = new Dictionary<string, string[]>
            {
                ["login"] = new[]
                {
                    "Login should contain only letters or digits"
                }
            };

            return new ObjectResult(errors)
            {
                StatusCode = 422,
                ContentTypes = { "application/json" }
            };
        }

        var userEntity = _mapper.Map<UserEntity>(user);
        _userRepository.Insert(userEntity);

        var createdDto = _mapper.Map<UserDto>(userEntity);

        if (Request.Headers.Accept.Any(
                value => value?.Contains(
                    "application/xml",
                    StringComparison.OrdinalIgnoreCase) == true))
        {
            return CreatedAtRoute(
                nameof(GetUserById),
                new { userId = userEntity.Id },
                userEntity.Id
            );
        }

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