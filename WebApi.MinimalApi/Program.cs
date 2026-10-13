using WebApi.MinimalApi.Domain;
using AutoMapper;
using Microsoft.AspNetCore.Mvc.Formatters;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System.Buffers;
using WebApi.MinimalApi.Models;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://localhost:5000");

// регаем репозиторий юзера
builder.Services.AddSingleton<IUserRepository, InMemoryUserRepository>();

// AutoMapper настраиваем
builder.Services.AddAutoMapper(cfg =>
{
    cfg.CreateMap<UserEntity, UserDto>()
       .ForMember(dest => dest.FullName, opt => opt.MapFrom(src => $"{src.LastName} {src.FirstName}"));
       cfg.CreateMap<UserEntity, UpdateDto>().ReverseMap(); // это для UpdateDto
}, new System.Reflection.Assembly[0]);


// контроллеры, JSON, XML и статус 406
builder.Services.AddControllers(options =>
{
    // разрешаем XML
    options.OutputFormatters.Add(new XmlSerializerOutputFormatter());
    
    // ставим json на первое место с CamelCase нотацией
    options.OutputFormatters.Insert(0, new NewtonsoftJsonOutputFormatter(
        new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver()
        }, ArrayPool<char>.Shared, options));

    // вернуть 406 для неизвестных форматов
    options.ReturnHttpNotAcceptable = true;
    
    // игнорить заголовок Accept при */* и возвращать json
    options.RespectBrowserAcceptHeader = true;
})
.ConfigureApiBehaviorOptions(options => {
    options.SuppressModelStateInvalidFilter = true;
    options.SuppressMapClientErrors = true;
});

var app = builder.Build();

app.MapControllers();

app.Run();