# Validation Strategy

This document outlines the validation architecture and best practices for the TargetBrowse application.

## Overview

TargetBrowse employs a multi-layered validation approach that ensures data integrity at multiple levels:

1. **Model-level validation** (DataAnnotations)
2. **Custom validation methods** (Model.Validate())
3. **Service-level validation** (Business logic)
4. **Format validation utilities** (YouTube IDs, URLs)

## Validation Layers

### Layer 1: DataAnnotations (Model Properties)

DataAnnotations provide declarative validation at the model level and are used by ASP.NET Core's model binding.

**Location:** Model properties (Features/*/Models/*.cs, Data/Entities/*.cs)

**Purpose:**
- Basic property-level validation (required, length, range)
- Client-side validation in forms
- Automatic validation during model binding

**Example:**
```csharp
[Required(ErrorMessage = "Please select a star rating")]
[Range(RatingValidator.MinStars, RatingValidator.MaxStars,
    ErrorMessage = "Rating must be between 1 and 5 stars")]
public int Stars { get; set; }
```

**Best Practices:**
- ✅ **DO** reference constants from `RatingValidator` for validation ranges
- ✅ **DO** provide clear, user-friendly error messages
- ✅ **DO** use DataAnnotations for simple property validation
- ❌ **DON'T** hardcode validation values (use constants)

### Layer 2: Custom Validation Methods (Model.Validate())

Custom validation methods handle complex validation logic that can't be expressed with DataAnnotations.

**Location:** Rating model classes implementing `IRatingModel`

**Purpose:**
- Multi-field validation
- Business rule validation
- Format validation (YouTube IDs)
- Contextual validation

**Example:**
```csharp
public List<string> Validate()
{
    var errors = new List<string>();

    if (VideoId == Guid.Empty)
        errors.Add("Video ID is required");

    if (!string.IsNullOrWhiteSpace(YouTubeVideoId) &&
        !YouTubeVideoParser.IsValidVideoId(YouTubeVideoId))
        errors.Add("YouTube video ID format is invalid");

    // Use shared rating validator
    errors.AddRange(RatingValidator.ValidateRating(Stars, Notes));

    return errors;
}
```

**Best Practices:**
- ✅ **DO** use shared validation utilities (`RatingValidator`, parsers)
- ✅ **DO** return all validation errors (not just the first one)
- ✅ **DO** validate format using utility classes
- ❌ **DON'T** duplicate validation logic between models

### Layer 3: Service-Level Validation (Business Logic)

Service-level validation enforces business rules and entity existence checks.

**Location:** Service classes (Services/*.cs, Features/*/Services/*.cs)

**Purpose:**
- Entity existence validation
- Permission/authorization checks
- Database-dependent validation
- Complex business rules

**Example:**
```csharp
protected override async Task<(bool CanRate, List<string> ErrorMessages)>
    ValidateCanRateAsync(string userId, Guid entityId)
{
    var errors = new List<string>();

    if (string.IsNullOrWhiteSpace(userId))
        errors.Add("User must be authenticated to rate videos");

    if (entityId == Guid.Empty)
        errors.Add("Invalid video identifier");

    // Check if video exists in database
    if (errors.Count == 0)
    {
        var videoExists = await Context.Videos.AnyAsync(v => v.Id == entityId);
        if (!videoExists)
            errors.Add("Video not found");
    }

    return (errors.Count == 0, errors);
}
```

**Best Practices:**
- ✅ **DO** validate entity existence before operations
- ✅ **DO** check user permissions
- ✅ **DO** use `RatingValidationResult` for consistent result handling
- ❌ **DON'T** mix business logic with model validation

### Layer 4: Format Validation Utilities

Specialized utilities for validating external identifiers and formats.

**Location:**
- `Services/Validation/RatingValidator.cs` - Rating validation
- `Features/Videos/Utilities/YouTubeVideoParser.cs` - Video ID validation
- `Features/Channels/Utilities/YouTubeUrlParser.cs` - Channel ID validation

**Purpose:**
- YouTube ID format validation
- URL parsing and validation
- Domain-specific format checks

**Example:**
```csharp
// Video ID validation
if (!YouTubeVideoParser.IsValidVideoId(videoId))
    errors.Add("Invalid YouTube video ID format");

// Channel ID validation
if (!YouTubeUrlParser.IsValidChannelId(channelId))
    errors.Add("Invalid YouTube channel ID format");
```

**Best Practices:**
- ✅ **DO** use dedicated utilities for format validation
- ✅ **DO** validate before using external IDs
- ✅ **DO** provide specific error messages
- ❌ **DON'T** duplicate format validation logic

## Shared Validation Components

### RatingValidator (Services/Validation/RatingValidator.cs)

Central validation utility for all rating-related validation.

**Constants:**
```csharp
public const int MinStars = 1;
public const int MaxStars = 5;
public const int MinNotesLength = 10;
public const int MaxNotesLength = 1000;
```

**Methods:**
- `ValidateStars(int stars)` - Validates star rating value
- `ValidateNotes(string notes)` - Validates notes content
- `ValidateRating(int stars, string notes)` - Validates both
- `CleanNotes(string notes)` - Trims and cleans notes
- `GetStarDisplayText(int stars)` - Display text for ratings
- `GetStarCssClass(int stars)` - CSS class for ratings

**Usage:**
```csharp
// In model validation
errors.AddRange(RatingValidator.ValidateRating(Stars, Notes));

// In model cleanup
Notes = RatingValidator.CleanNotes(Notes);
```

### IRatingModel Interface (Services/Validation/IRatingModel.cs)

Interface defining common rating model behavior.

**Properties:**
- `int Stars` - Star rating value
- `string Notes` - Rating notes
- `bool IsValid` - Validation status
- `string StarDisplayText` - Display text
- `string StarCssClass` - CSS class

**Methods:**
- `List<string> Validate()` - Validation logic
- `void CleanNotes()` - Notes cleanup

**Implemented by:**
- `RateVideoModel`
- `RateChannelModel`

### RatingValidationResult (Services/Validation/RatingValidationResult.cs)

Result object for validation operations.

**Properties:**
- `bool CanRate` - Validation success status
- `List<string> ErrorMessages` - Validation errors
- `string PrimaryError` - First error message
- `string AllErrors` - All errors joined

**Factory Methods:**
```csharp
RatingValidationResult.Success()
RatingValidationResult.Failure("Error message")
RatingValidationResult.Failure(errorList)
```

## Validation Flow

### Rating Creation Flow

```
1. User submits form
   ↓
2. DataAnnotations validate (model binding)
   ↓
3. Controller calls Model.Validate()
   ↓
4. Service.ValidateCanRateAsync() (business logic)
   ↓
5. Service.CreateRatingAsync()
   ↓
6. Database constraints enforce final validation
```

### Error Handling Strategy

**Model Validation:**
- Return `List<string>` of all validation errors
- Display all errors to user
- Allow user to fix all issues at once

**Service Validation:**
- Return `RatingValidationResult` with success/failure
- Log validation failures
- Show user-friendly error messages via MessageCenter

**Database Validation:**
- Catch constraint violations
- Map to user-friendly messages
- Log technical details for debugging

## Adding New Validation

### For Rating Fields

1. Add constant to `RatingValidator` if needed
2. Add validation method to `RatingValidator`
3. Reference in DataAnnotations using constants
4. Use in model `Validate()` method
5. Update interface if method signature changes

### For New Entity Types

1. Add DataAnnotations to entity properties
2. Create model implementing `IRatingModel` if rating-related
3. Implement `Validate()` method
4. Add service-level validation in service class
5. Use `RatingValidationResult` for consistency

### For Format Validation

1. Create utility class in appropriate feature folder
2. Add `IsValid*()` static methods
3. Add parsing/extraction methods
4. Use in model validation
5. Document format requirements

## Common Patterns

### Pattern 1: Using Shared Constants

```csharp
// BAD - Hardcoded values
[Range(1, 5)]
[StringLength(1000, MinimumLength = 10)]

// GOOD - Shared constants
[Range(RatingValidator.MinStars, RatingValidator.MaxStars)]
[StringLength(RatingValidator.MaxNotesLength,
    MinimumLength = RatingValidator.MinNotesLength)]
```

### Pattern 2: Format Validation

```csharp
// In model Validate() method
if (!string.IsNullOrWhiteSpace(YouTubeVideoId))
{
    if (!YouTubeVideoParser.IsValidVideoId(YouTubeVideoId))
        errors.Add("YouTube video ID format is invalid");
}
```

### Pattern 3: Service Validation Result

```csharp
public async Task<RatingValidationResult> ValidateCanRateAsync(
    string userId, Guid entityId)
{
    var (canRate, errors) = await ValidateCanRateAsync(userId, entityId);
    return canRate
        ? RatingValidationResult.Success()
        : RatingValidationResult.Failure(errors.ToArray());
}
```

### Pattern 4: Generic Validation Method

```csharp
// Base class with interface constraint
public abstract class RatingServiceBase<TRatingModel, TRateModel>
    where TRateModel : class, IRatingModel
{
    protected void CleanNotes(TRateModel ratingModel)
    {
        ratingModel.CleanNotes();
    }
}
```

## Testing Validation

### Unit Tests

- Test each validation method independently
- Test boundary conditions (min, max values)
- Test format validators with valid/invalid inputs
- Test error message content

### Integration Tests

- Test full validation flow
- Test service-level validation with database
- Test validation error handling
- Test validation result objects

## Migration Guide

### Updating Existing Validation

1. **Identify hardcoded validation values**
2. **Move to `RatingValidator` constants**
3. **Update DataAnnotations to reference constants**
4. **Add format validation if missing**
5. **Test all validation paths**

### Example Migration

Before:
```csharp
[Range(1, 5)]
public int Stars { get; set; }

public List<string> Validate()
{
    var errors = new List<string>();
    if (Stars < 1 || Stars > 5)
        errors.Add("Invalid stars");
    return errors;
}
```

After:
```csharp
[Range(RatingValidator.MinStars, RatingValidator.MaxStars)]
public int Stars { get; set; }

public List<string> Validate()
{
    var errors = new List<string>();
    errors.AddRange(RatingValidator.ValidateRating(Stars, Notes));
    return errors;
}
```

## Validation Checklist

When adding new validation:

- [ ] Constants defined in appropriate validator class
- [ ] DataAnnotations reference constants
- [ ] Custom validation in model `Validate()` method
- [ ] Format validation using utility classes
- [ ] Service-level validation for business rules
- [ ] User-friendly error messages
- [ ] Validation result objects used
- [ ] All validation layers tested
- [ ] Documentation updated

## References

- `Services/Validation/RatingValidator.cs` - Core rating validation
- `Services/Validation/IRatingModel.cs` - Rating model interface
- `Services/Validation/RatingValidationResult.cs` - Validation results
- `Services/RatingServiceBase.cs` - Service validation patterns
- `Features/Videos/Utilities/YouTubeVideoParser.cs` - Video ID validation
- `Features/Channels/Utilities/YouTubeUrlParser.cs` - Channel ID validation
