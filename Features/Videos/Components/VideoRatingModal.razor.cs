using Microsoft.AspNetCore.Components;
using TargetBrowse.Features.Videos.Models;

namespace TargetBrowse.Features.Videos.Components;

public partial class VideoRatingModal : ComponentBase
{
    /// <summary>
    /// Whether the modal is visible.
    /// </summary>
    [Parameter] public bool IsVisible { get; set; }

    /// <summary>
    /// The rating model for the form.
    /// </summary>
    [Parameter] public RateVideoModel? Model { get; set; }

    /// <summary>
    /// Callback when the modal should be closed.
    /// </summary>
    [Parameter] public EventCallback OnClose { get; set; }

    /// <summary>
    /// Callback when a rating is submitted.
    /// </summary>
    [Parameter] public EventCallback<RateVideoModel> OnSubmit { get; set; }

    /// <summary>
    /// Callback when a rating should be deleted.
    /// </summary>
    [Parameter] public EventCallback<RateVideoModel> OnDelete { get; set; }

    /// <summary>
    /// Whether to allow clicking backdrop to close modal.
    /// </summary>
    [Parameter] public bool CloseOnBackdropClick { get; set; } = true;

    private bool IsSubmitting = false;
    private bool IsDeleting = false;
    private bool ShowDeleteConfirm = false;

    /// <summary>
    /// Handles valid form submission.
    /// </summary>
    private async Task HandleValidSubmit()
    {
        if (Model == null || IsSubmitting) return;

        IsSubmitting = true;
        StateHasChanged();

        try
        {
            await OnSubmit.InvokeAsync(Model);
        }
        catch (Exception)
        {
            // Error handling should be done in parent component
            // and communicated via message center
        }
        finally
        {
            IsSubmitting = false;
            StateHasChanged();
        }
    }

    /// <summary>
    /// Handles notes input for real-time character count.
    /// </summary>
    private void OnNotesInput(ChangeEventArgs e)
    {
        if (Model != null && e.Value != null)
        {
            Model.Notes = e.Value.ToString() ?? string.Empty;
            StateHasChanged();
        }
    }

    /// <summary>
    /// Closes the modal.
    /// </summary>
    private async Task CloseModal()
    {
        if (IsSubmitting || IsDeleting) return;
        await OnClose.InvokeAsync();
    }

    /// <summary>
    /// Handles backdrop click events.
    /// </summary>
    private async Task HandleBackdropClick()
    {
        if (CloseOnBackdropClick && !IsSubmitting && !IsDeleting)
        {
            await CloseModal();
        }
    }

    /// <summary>
    /// Shows delete confirmation dialog.
    /// </summary>
    private void ShowDeleteConfirmation()
    {
        ShowDeleteConfirm = true;
        StateHasChanged();
    }

    /// <summary>
    /// Hides delete confirmation dialog.
    /// </summary>
    private void HideDeleteConfirmation()
    {
        ShowDeleteConfirm = false;
        StateHasChanged();
    }

    /// <summary>
    /// Handles rating deletion.
    /// </summary>
    private async Task HandleDelete()
    {
        if (Model == null || IsDeleting) return;

        IsDeleting = true;
        StateHasChanged();

        try
        {
            await OnDelete.InvokeAsync(Model);
            ShowDeleteConfirm = false;
        }
        catch (Exception)
        {
            // Error handling should be done in parent component
        }
        finally
        {
            IsDeleting = false;
            StateHasChanged();
        }
    }

    /// <summary>
    /// Handles ESC key press to close modal.
    /// </summary>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender && IsVisible)
        {
            // Focus the first form element for accessibility
            await Task.Delay(100); // Small delay to ensure modal is rendered
        }
    }
}