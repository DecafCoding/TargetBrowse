using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;
using TargetBrowse.Features.Channels.Models;
using TargetBrowse.Features.Channels.Services;

namespace TargetBrowse.Features.Channels.Components;

/// <summary>
/// Modal component for creating and editing channel ratings.
/// Supports both new ratings and editing existing ratings with delete functionality.
/// </summary>
public partial class ChannelRatingModal : ComponentBase
{
    [Inject]
    protected IChannelRatingService ChannelRatingService { get; set; } = default!;

    #region Parameters and Cascading Parameters

    [CascadingParameter]
    private Task<AuthenticationState>? AuthenticationStateTask { get; set; }

    [Parameter]
    public EventCallback<ChannelRatingModel> OnRatingCreated { get; set; }

    [Parameter]
    public EventCallback<ChannelRatingModel> OnRatingUpdated { get; set; }

    [Parameter]
    public EventCallback<Guid> OnRatingDeleted { get; set; }

    #endregion

    #region State Properties

    public bool IsVisible { get; private set; } = false;
    private bool IsSubmitting { get; set; } = false;
    private bool IsDeleting { get; set; } = false;
    private bool IsEditMode => ExistingRating != null;

    private ChannelDisplayModel? CurrentChannel { get; set; }
    private ChannelRatingModel? ExistingRating { get; set; }
    private RateChannelModel RatingModel { get; set; } = new();

    #endregion

    #region Computed Properties

    private int NotesCharacterCount => RatingModel.Notes?.Length ?? 0;
    private bool IsFormValid => RatingModel.IsValid && NotesCharacterCount >= 10;

    #endregion

    #region Public Methods

    /// <summary>
    /// Shows the modal for creating a new rating.
    /// </summary>
    /// <param name="channel">The channel to rate</param>
    public void ShowForNewRating(ChannelDisplayModel channel)
    {
        CurrentChannel = channel;
        ExistingRating = null;
        RatingModel = RateChannelModel.CreateNew(channel.Id, channel.YouTubeChannelId, channel.Name);
        IsVisible = true;
        StateHasChanged();
    }

    /// <summary>
    /// Shows the modal for editing an existing rating.
    /// </summary>
    /// <param name="channel">The channel being rated</param>
    /// <param name="existingRating">The existing rating to edit</param>
    public void ShowForEditRating(ChannelDisplayModel channel, ChannelRatingModel existingRating)
    {
        CurrentChannel = channel;
        ExistingRating = existingRating;
        RatingModel = RateChannelModel.CreateUpdate(existingRating);
        IsVisible = true;
        StateHasChanged();
    }

    /// <summary>
    /// Closes the modal and resets state.
    /// </summary>
    public void CloseModal()
    {
        //if (IsSubmitting || IsDeleting) return;

        IsVisible = false;
        CurrentChannel = null;
        ExistingRating = null;
        RatingModel = new();
        StateHasChanged();
    }

    #endregion

    #region Event Handlers

    /// <summary>
    /// Handles star rating changes from the StarRatingInput component.
    /// </summary>
    /// <param name="newRating">The new star rating value</param>
    private void OnStarRatingChanged(int newRating)
    {
        RatingModel.Stars = newRating;
        StateHasChanged();
    }

    /// <summary>
    /// Handles form submission for creating or updating ratings.
    /// Validates form state and calls appropriate service method.
    /// </summary>
    private async Task HandleValidSubmit()
    {
        if (IsSubmitting || !IsFormValid || CurrentChannel == null) return;

        try
        {
            IsSubmitting = true;
            StateHasChanged();

            var authState = await AuthenticationStateTask!;
            var userId = authState?.User?.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(userId))
            {
                return;
            }

            ChannelRatingModel result;

            if (IsEditMode && ExistingRating != null)
            {
                result = await ChannelRatingService.UpdateRatingAsync(userId, ExistingRating.Id, RatingModel);
                if (OnRatingUpdated.HasDelegate)
                {
                    await OnRatingUpdated.InvokeAsync(result);
                }
            }
            else
            {
                result = await ChannelRatingService.CreateRatingAsync(userId, RatingModel);
                if (OnRatingCreated.HasDelegate)
                {
                    await OnRatingCreated.InvokeAsync(result);
                }
            }

            CloseModal();
        }
        catch (Exception)
        {
            // Error handling is done in the service layer via MessageCenterService
            // We don't need to display errors here as they're handled globally
        }
        finally
        {
            IsSubmitting = false;
            StateHasChanged();
        }
    }

    /// <summary>
    /// Handles rating deletion.
    /// Confirms with user and calls service to delete the rating.
    /// </summary>
    private async Task HandleDelete()
    {
        if (IsDeleting || ExistingRating == null) return;

        try
        {
            IsDeleting = true;
            StateHasChanged();

            var authState = await AuthenticationStateTask!;
            var userId = authState?.User?.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(userId))
            {
                return;
            }

            var success = await ChannelRatingService.DeleteRatingAsync(userId, ExistingRating.Id);

            if (success)
            {
                if (OnRatingDeleted.HasDelegate)
                {
                    await OnRatingDeleted.InvokeAsync(ExistingRating.Id);
                }
                CloseModal();
            }
        }
        catch (Exception)
        {
            // Error handling is done in the service layer via MessageCenterService
        }
        finally
        {
            IsDeleting = false;
            StateHasChanged();
        }
    }

    /// <summary>
    /// Handles notes input changes for real-time character count updates.
    /// </summary>
    /// <param name="e">The change event args containing the new text value</param>
    private void OnNotesChanged(ChangeEventArgs e)
    {
        RatingModel.Notes = e.Value?.ToString() ?? string.Empty;
        StateHasChanged();
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Gets the appropriate CSS class for the star rating badge based on rating value.
    /// </summary>
    /// <returns>Bootstrap badge class for the current star rating</returns>
    private string GetStarBadgeClass()
    {
        return RatingModel.Stars switch
        {
            1 => "bg-danger",
            2 => "bg-warning",
            3 => "bg-info",
            4 => "bg-success",
            5 => "bg-success",
            _ => "bg-secondary"
        };
    }

    #endregion
}