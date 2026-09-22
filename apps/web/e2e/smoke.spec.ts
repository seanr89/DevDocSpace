import { expect, test } from "@playwright/test";

test("home page renders and links to docs and APIs", async ({ page }) => {
  await page.goto("/");
  await expect(page.getByRole("heading", { name: "DevDocSpace" })).toBeVisible();
  await expect(page.getByRole("link", { name: "Browse docs" })).toBeVisible();
  await expect(page.getByRole("link", { name: "Explore APIs" })).toBeVisible();
});

test("protected routes redirect unauthenticated users to sign-in", async ({ page }) => {
  await page.goto("/apis/petstore/v1?env=staging");
  await expect(page).toHaveURL(/\/sign-in\?next=%2Fapis%2Fpetstore%2Fv1/);
  await expect(page.getByRole("heading", { name: "Sign in" })).toBeVisible();
});

// The dev server runs without Firebase configured, so the sign-in page offers the local dev-auth presets.
test("dev sign-in presets are offered and sign the user in", async ({ page }) => {
  await page.goto("/sign-in?next=%2Fapis");
  await expect(page.getByText("Local development sign-in")).toBeVisible();
  await expect(page.getByRole("button", { name: "Continue with Google" })).toHaveCount(0);
  await page.getByRole("button", { name: "Internal developer" }).click();
  await expect(page).toHaveURL(/\/apis$/);
  await expect(page.getByText("dev@devdocspace.local")).toBeVisible();
  await expect(page.getByText("dev auth")).toBeVisible();
});

test("dev sign-out returns to signed-out state", async ({ page }) => {
  await page.goto("/sign-in");
  await page.getByRole("button", { name: "Admin" }).click();
  await expect(page).toHaveURL(/\/docs$/);
  await page.getByRole("button", { name: "Sign out" }).click();
  await expect(page.getByRole("link", { name: "Sign in" })).toBeVisible();
});

test("sign-in page has no console errors on load", async ({ page }) => {
  const errors: string[] = [];
  page.on("pageerror", (e) => errors.push(e.message));
  await page.goto("/sign-in");
  await expect(page.getByRole("heading", { name: "Sign in" })).toBeVisible();
  expect(errors).toEqual([]);
});
