// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/gtg-npc-ml.md, section "lib.rs"
// ============================================================================

//! ML integration boundary only. No framework, weights or training are bundled.
//! Future implementations must preserve deterministic non-ML fallback.

/// Whether and how an NPC's model may change.
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum LearningMode {
    /// No model.
    Disabled,
    /// A model whose weights never change.
    FixedWeights,
    /// A model that may update its weights in play.
    OnlineUpdates,
}

/// A model that turns an observation vector into action scores.
pub trait Policy {
    /// Fills `actions` with a score per action for `observation`.
    ///
    /// # Errors
    ///
    /// Returns [`PolicyError::InvalidShape`] when a slice has the wrong length, and
    /// [`PolicyError::Unsupported`] when the policy cannot run.
    fn infer(&self, observation: &[f32], actions: &mut [f32]) -> Result<(), PolicyError>;
}

/// Why a policy could not run.
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum PolicyError {
    /// No model is available for this request.
    Unsupported,
    /// An input or output slice had the wrong length.
    InvalidShape,
}
