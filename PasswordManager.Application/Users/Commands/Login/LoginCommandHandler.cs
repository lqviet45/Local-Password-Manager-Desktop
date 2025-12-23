using Microsoft.Extensions.Logging;
using PasswordManager.Application.Common.Mapping;
using PasswordManager.Domain.Interfaces;
using PasswordManager.Shared.Common.Result;
using PasswordManager.Shared.Users.Commands.Login;
using PasswordManager.Shared.Users.Dto;

namespace PasswordManager.Application.Users.Commands.Login;

public sealed class LoginCommandHandler : PasswordManager.Shared.Core.Message.ICommandHandler<LoginCommand, LoginResultDto>
{
    private readonly IUserRepository _userRepository;
    private readonly ICryptoProvider _cryptoProvider;
    private readonly ILogger<LoginCommandHandler> _logger;

    private const int LockoutThreshold = 5;

    public LoginCommandHandler(
        IUserRepository userRepository,
        ICryptoProvider cryptoProvider,
        ILogger<LoginCommandHandler> logger)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _cryptoProvider = cryptoProvider ?? throw new ArgumentNullException(nameof(cryptoProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result<LoginResultDto>> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        var user = await _userRepository.GetByEmailAsync(email, isTracked: true, cancellationToken);
        if (user == null)
        {
            _logger.LogWarning("Login failed - user not found for email {Email}", email);
            return Result<LoginResultDto>.Failure("Invalid email or master password");
        }

        if (user.IsLocked)
        {
            _logger.LogWarning("Login blocked - account locked for {Email}", email);
            return Result<LoginResultDto>.Failure("Account is locked. Please try again later.");
        }

        var passwordValid = await _cryptoProvider.VerifyPasswordAsync(request.MasterPassword, user.MasterPasswordHash);
        if (!passwordValid)
        {
            user.FailedLoginAttempts += 1;
            user.LastFailedLoginUtc = DateTime.UtcNow;
            user.IsLocked = user.FailedLoginAttempts >= LockoutThreshold;

            await _userRepository.UpdateAsync(user, cancellationToken);

            _logger.LogWarning("Login failed - invalid password for {Email}. Attempts: {Attempts}", email,
                user.FailedLoginAttempts);
            return Result<LoginResultDto>.Failure("Invalid email or master password");
        }

        if (user.FailedLoginAttempts > 0)
        {
            user.FailedLoginAttempts = 0;
            user.LastFailedLoginUtc = null;
        }

        user.LastLoginUtc = DateTime.UtcNow;
        await _userRepository.UpdateAsync(user, cancellationToken);

        var dto = new LoginResultDto
        {
            User = user.ToDto(),
            Salt = user.Salt,
            EncryptedMasterKey = user.EncryptedMasterKey
        };

        _logger.LogInformation("Login succeeded for {Email}", email);
        return Result<LoginResultDto>.Success(dto);
    }
}

