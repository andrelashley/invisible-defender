## Data Model

Synthetic events that include a timestamp, type of action (offensive vs defensive), team, player, and outcome.

MatchEvent
- Id
- MatchId
- Timestamp
- EventType
- TeamId
- PlayerId
- Outcome
- BallPosition
- PlayerPositions[]

Possible EventTypes for MatchEvent:
Pass
Shot
Tackle
Interception
Pressure
BallRecovery
PossessionChange
PositionSnapshot