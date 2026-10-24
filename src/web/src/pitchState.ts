export type Point = { x: number; y: number };
export type Player = { playerId: string; teamId: string; position: Point };
export type MatchEvent = { id: string; matchId: string; timestamp: number; eventType: string; teamId: string; playerId: string; outcome: string; ballPosition: Point; playerPositions: Player[]; scenario?: string; targetPlayerId?: string; ballInFlight?: boolean; ballCarrierId?: string | null; ballFrom?: Point; ballTo?: Point };
export type Contribution = { id: string; type: string; eventId: string; defenderId: string; receiverId: string; start: Point; end: Point; defenderPosition: Point; distanceMetres: number; corridorMetres: number; explanation: string };
export type Measurement = { eventId: string; receiverId?: string; ballPosition: Point; receiverPosition?: Point; blockerIds: string[]; defenderDistanceMetres?: number; corridorMetres: number; defenderPosition?: Point };
export type DefensiveInsight = { id: string; matchId: string; insightType: string; startTimestamp: number; endTimestamp: number; primaryDefenderId?: string; attackingPlayerId?: string; affectedReceiverId?: string; supportingEventIds: string[]; title: string; explanation: string; significanceScore: number; isKeyMoment: boolean; significanceFactors: { reason: string; points: number }[]; observation: MatchEvent; triggerEvent: MatchEvent; supportingObservations: MatchEvent[]; actualMeasurements: Measurement; previousMeasurements?: Measurement };
export type MatchState = { revision: number; status: string; frameIndex: number; frameCount: number; event: MatchEvent; contributions: Contribution[]; insights?: DefensiveInsight[]; events?: MatchState[] };

export type PitchMode = 'live' | 'hypothetical';
export type Override = { defenderId: string; position: Point };
export type PitchState = {
  liveState?: MatchState;
  pitchMode: PitchMode;
  hypotheticalFrame: MatchEvent | null;
  override: Override | null;
};
export const initialPitchState: PitchState = { pitchMode: 'live', hypotheticalFrame: null, override: null };
export type PitchAction =
  | { type: 'live'; next: MatchState }
  | { type: 'hypothetical'; frame: MatchEvent; override: Override | null }
  | { type: 'move'; position: Point }
  | { type: 'returnToLive' };
export function pitchReducer(state: PitchState, action: PitchAction): PitchState {
  switch (action.type) {
    case 'live': return !state.liveState || action.next.revision >= state.liveState.revision
      ? { ...state, liveState: action.next } : state;
    // Evidence inspection and defender dragging share one frozen observation.
    case 'hypothetical': return { ...state, pitchMode: 'hypothetical',
      hypotheticalFrame: state.hypotheticalFrame ?? action.frame, override: action.override };
    case 'move': return state.pitchMode === 'hypothetical' && state.override
      ? { ...state, override: { ...state.override, position: action.position } } : state;
    case 'returnToLive': return { ...state, pitchMode: 'live', hypotheticalFrame: null, override: null };
  }
}
export function displayedFrame(state: PitchState): MatchEvent | undefined | null {
  const liveFrame = state.liveState?.event;
  return state.pitchMode === 'hypothetical' ? state.hypotheticalFrame : liveFrame;
}
