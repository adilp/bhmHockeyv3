using BHMHockey.Api.Services;
using FluentAssertions;
using Xunit;

namespace BHMHockey.Api.Tests.Services;

/// <summary>
/// Tests for TournamentStateMachine - TDD approach for TRN-002.
/// Tests written FIRST before implementation.
/// </summary>
public class TournamentStateMachineTests
{
    #region CanTransition Tests - Valid Transitions

    [Fact]
    public void CanTransition_DraftToOpen_ReturnsTrue()
    {
        TournamentStateMachine.CanTransition("Draft", "Open").Should().BeTrue();
    }

    [Fact]
    public void CanTransition_DraftToCancelled_ReturnsTrue()
    {
        TournamentStateMachine.CanTransition("Draft", "Cancelled").Should().BeTrue();
    }

    [Fact]
    public void CanTransition_OpenToRegistrationClosed_ReturnsTrue()
    {
        TournamentStateMachine.CanTransition("Open", "RegistrationClosed").Should().BeTrue();
    }

    [Fact]
    public void CanTransition_OpenToCancelled_ReturnsTrue()
    {
        TournamentStateMachine.CanTransition("Open", "Cancelled").Should().BeTrue();
    }

    [Fact]
    public void CanTransition_RegistrationClosedToInProgress_ReturnsTrue()
    {
        TournamentStateMachine.CanTransition("RegistrationClosed", "InProgress").Should().BeTrue();
    }

    [Fact]
    public void CanTransition_RegistrationClosedToCancelled_ReturnsTrue()
    {
        TournamentStateMachine.CanTransition("RegistrationClosed", "Cancelled").Should().BeTrue();
    }

    [Fact]
    public void CanTransition_InProgressToCompleted_ReturnsTrue()
    {
        TournamentStateMachine.CanTransition("InProgress", "Completed").Should().BeTrue();
    }

    [Fact]
    public void CanTransition_InProgressToPostponed_ReturnsTrue()
    {
        TournamentStateMachine.CanTransition("InProgress", "Postponed").Should().BeTrue();
    }

    [Fact]
    public void CanTransition_InProgressToCancelled_ReturnsTrue()
    {
        TournamentStateMachine.CanTransition("InProgress", "Cancelled").Should().BeTrue();
    }

    [Fact]
    public void CanTransition_PostponedToInProgress_ReturnsTrue()
    {
        TournamentStateMachine.CanTransition("Postponed", "InProgress").Should().BeTrue();
    }

    [Fact]
    public void CanTransition_PostponedToCancelled_ReturnsTrue()
    {
        TournamentStateMachine.CanTransition("Postponed", "Cancelled").Should().BeTrue();
    }

    #endregion

    #region CanTransition Tests - Invalid Transitions

    [Fact]
    public void CanTransition_CompletedToAnyState_ReturnsFalse()
    {
        // Completed is a terminal state
        TournamentStateMachine.CanTransition("Completed", "Draft").Should().BeFalse();
        TournamentStateMachine.CanTransition("Completed", "Open").Should().BeFalse();
        TournamentStateMachine.CanTransition("Completed", "InProgress").Should().BeFalse();
        TournamentStateMachine.CanTransition("Completed", "Cancelled").Should().BeFalse();
    }

    [Fact]
    public void CanTransition_CancelledToAnyState_ReturnsFalse()
    {
        // Cancelled is a terminal state
        TournamentStateMachine.CanTransition("Cancelled", "Draft").Should().BeFalse();
        TournamentStateMachine.CanTransition("Cancelled", "Open").Should().BeFalse();
        TournamentStateMachine.CanTransition("Cancelled", "InProgress").Should().BeFalse();
        TournamentStateMachine.CanTransition("Cancelled", "Completed").Should().BeFalse();
    }

    [Fact]
    public void CanTransition_DraftToInProgress_ReturnsFalse()
    {
        // Must go through Open first
        TournamentStateMachine.CanTransition("Draft", "InProgress").Should().BeFalse();
    }

    [Fact]
    public void CanTransition_OpenToInProgress_ReturnsFalse()
    {
        // Must go through RegistrationClosed first
        TournamentStateMachine.CanTransition("Open", "InProgress").Should().BeFalse();
    }

    [Fact]
    public void CanTransition_OpenToCompleted_ReturnsFalse()
    {
        // Must go through InProgress first
        TournamentStateMachine.CanTransition("Open", "Completed").Should().BeFalse();
    }

    [Fact]
    public void CanTransition_DraftToCompleted_ReturnsFalse()
    {
        TournamentStateMachine.CanTransition("Draft", "Completed").Should().BeFalse();
    }

    [Fact]
    public void CanTransition_PostponedToCompleted_ReturnsFalse()
    {
        // Must resume to InProgress first
        TournamentStateMachine.CanTransition("Postponed", "Completed").Should().BeFalse();
    }

    [Fact]
    public void CanTransition_SameStateToDraft_ReturnsFalse()
    {
        TournamentStateMachine.CanTransition("Draft", "Draft").Should().BeFalse();
    }

    [Fact]
    public void CanTransition_InvalidFromState_ReturnsFalse()
    {
        TournamentStateMachine.CanTransition("InvalidState", "Open").Should().BeFalse();
    }

    #endregion

    #region ValidateTransition Tests

    [Fact]
    public void ValidateTransition_ValidTransition_DoesNotThrow()
    {
        var act = () => TournamentStateMachine.ValidateTransition("Draft", "Open");
        act.Should().NotThrow();
    }

    [Fact]
    public void ValidateTransition_InvalidTransition_ThrowsInvalidOperationException()
    {
        var act = () => TournamentStateMachine.ValidateTransition("Draft", "Completed");
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cannot transition*Draft*Completed*");
    }

    [Fact]
    public void ValidateTransition_FromTerminalState_ThrowsInvalidOperationException()
    {
        var act = () => TournamentStateMachine.ValidateTransition("Completed", "InProgress");
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cannot transition*Completed*InProgress*");
    }

    #endregion

    #region GetActionForTransition Tests

    [Fact]
    public void GetActionForTransition_DraftToOpen_ReturnsPublish()
    {
        TournamentStateMachine.GetActionForTransition("Draft", "Open").Should().Be("Publish");
    }

    [Fact]
    public void GetActionForTransition_OpenToRegistrationClosed_ReturnsCloseRegistration()
    {
        TournamentStateMachine.GetActionForTransition("Open", "RegistrationClosed").Should().Be("CloseRegistration");
    }

    [Fact]
    public void GetActionForTransition_RegistrationClosedToInProgress_ReturnsStart()
    {
        TournamentStateMachine.GetActionForTransition("RegistrationClosed", "InProgress").Should().Be("Start");
    }

    [Fact]
    public void GetActionForTransition_InProgressToCompleted_ReturnsComplete()
    {
        TournamentStateMachine.GetActionForTransition("InProgress", "Completed").Should().Be("Complete");
    }

    [Fact]
    public void GetActionForTransition_InProgressToPostponed_ReturnsPostpone()
    {
        TournamentStateMachine.GetActionForTransition("InProgress", "Postponed").Should().Be("Postpone");
    }

    [Fact]
    public void GetActionForTransition_PostponedToInProgress_ReturnsResume()
    {
        TournamentStateMachine.GetActionForTransition("Postponed", "InProgress").Should().Be("Resume");
    }

    [Fact]
    public void GetActionForTransition_AnyToCancelled_ReturnsCancel()
    {
        TournamentStateMachine.GetActionForTransition("Draft", "Cancelled").Should().Be("Cancel");
        TournamentStateMachine.GetActionForTransition("Open", "Cancelled").Should().Be("Cancel");
        TournamentStateMachine.GetActionForTransition("InProgress", "Cancelled").Should().Be("Cancel");
    }

    #endregion
}
