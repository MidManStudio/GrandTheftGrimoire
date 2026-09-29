//! ML integration boundary only. No framework, weights or training are bundled.
//! Future implementations must preserve deterministic non-ML fallback.

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum LearningMode {
    Disabled,
    FixedWeights,
    OnlineUpdates,
}

pub trait Policy {
    fn infer(&self, observation: &[f32], actions: &mut [f32]) -> Result<(), PolicyError>;
}

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum PolicyError {
    Unsupported,
    InvalidShape,
}
