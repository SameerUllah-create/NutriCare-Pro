namespace NutriCare.Application.Queries.Authentication;

using MediatR;
using NutriCare.Application.DTOs;

/// <summary>
/// Query to get user by id
/// </summary>
public class GetUserByIdQuery : IRequest<UserDto>
{
    public long UserId { get; set; }
}

/// <summary>
/// Query to get user by email
/// </summary>
public class GetUserByEmailQuery : IRequest<UserDto>
{
    public string Email { get; set; } = string.Empty;
}

/// <summary>
/// Query to get all users
/// </summary>
public class GetAllUsersQuery : IRequest<IEnumerable<UserDto>>
{
    public int Skip { get; set; } = 0;
    public int Take { get; set; } = 10;
}

/// <summary>
/// Query to verify token
/// </summary>
public class VerifyTokenQuery : IRequest<bool>
{
    public string Token { get; set; } = string.Empty;
}
