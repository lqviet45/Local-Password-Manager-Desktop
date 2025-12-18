namespace PasswordManager.Domain.Enums;

/// <summary>
/// Password strength levels based on entropy calculation
/// </summary>
public enum StrengthLevel
{
    /// <summary>
    /// Very weak password (< 40 bits entropy)
    /// </summary>
    VeryWeak = 0,
    
    /// <summary>
    /// Weak password (40-60 bits entropy)
    /// </summary>
    Weak = 1,
    
    /// <summary>
    /// Fair password (60-80 bits entropy)
    /// </summary>
    Fair = 2,
    
    /// <summary>
    /// Strong password (80-100 bits entropy)
    /// </summary>
    Strong = 3,
    
    /// <summary>
    /// Very strong password (> 100 bits entropy)
    /// </summary>
    VeryStrong = 4
}