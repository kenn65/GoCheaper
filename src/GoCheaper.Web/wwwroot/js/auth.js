// Called by AuthCookieService via JS interop.
// Uses fetch so the browser honours the Set-Cookie response header.

export async function signIn(userId, email, fullName, isDriver, isPassenger, accessToken, accessTokenExpiry, refreshToken, refreshTokenExpiry) {
    await fetch('/auth/signin', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ userId, email, fullName, isDriver, isPassenger, accessToken, accessTokenExpiry, refreshToken, refreshTokenExpiry })
    });
}

export async function signOut() {
    await fetch('/auth/signout', { method: 'POST' });
}
