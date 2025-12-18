using PasswordManager.Domain.Enums;

namespace PasswordManager.Domain.Interfaces;

/// <summary>
/// Service for evaluating password strength based on entropy and complexity.
/// </summary>
public interface IPasswordStrengthService
{
    /// <summary>
    /// Calculates password entropy in bits.
    /// </summary>
    /// <param name="password">Password to evaluate</param>
    /// <returns>Entropy value in bits</returns>
    double CalculateEntropy(string password);
    
    /// <summary>
    /// Calculates a strength score (0-100).
    /// </summary>
    /// <param name="password">Password to evaluate</param>
    /// <returns>Score from 0 (very weak) to 100 (very strong)</returns>
    int CalculateStrengthScore(string password);
    
    /// <summary>
    /// Determines the strength level category.
    /// </summary>
    /// <param name="password">Password to evaluate</param>
    /// <returns>Strength level enumeration</returns>
    StrengthLevel EvaluateStrength(string password);
    
    /// <summary>
    /// Provides detailed strength analysis with recommendations.
    /// </summary>
    /// <param name="password">Password to analyze</param>
    /// <returns>Analysis result with metrics and suggestions</returns>
    PasswordStrengthAnalysis AnalyzePassword(string password);
}

/// <summary>
/// Detailed password strength analysis result.
/// </summary>
public sealed record PasswordStrengthAnalysis
{
    public required double Entropy { get; init; }
    public required int Score { get; init; }
    public required StrengthLevel Level { get; init; }
    public required int Length { get; init; }
    public bool HasUppercase { get; init; }
    public bool HasLowercase { get; init; }
    public bool HasDigits { get; init; }
    public bool HasSpecialChars { get; init; }
    public bool HasRepeatingChars { get; init; }
    public bool HasSequentialChars { get; init; }
    public List<string> Suggestions { get; init; } = new();
}