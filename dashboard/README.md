# XRHands Therapist Console

Laptop viewer for Unity Cloud RWS session snapshots. Matches the current headset schema: game and non-game phase slots, demographics, Fitts / SPARC / LDLJ / covered area / symmetry, other-arm reach, and blue/orange reach paths with red wobble slashes.

## Run locally

```bash
cd dashboard
npm install
npm run dev
```

Open http://localhost:5173

## Sign in

- Therapist ID: `0000`
- Password: `0000`

If the User ID list is empty, the headset data is still private. On this laptop:

1. Open the Unity project (stay signed in with the account that can see Cloud Save).
2. **XRHands → Export Cloud Save to Therapist Dashboard**
3. On the website, click **Reload**.

## Review

- **Game / Non-game** toggles `phases[]` vs `plainPhases[]`.
- **Compare** shows the same metric table as the in-headset game vs non-game panel.
- Selecting a phase opens Fitts, endpoint scatter, RWS quadrants, SPARC, covered area, and SI.
- Click a voxel marker to draw that reach (blue outbound, orange return, red wobble slash).

