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
  await expect(page.getByRole("button", { name: "Continue with Google" })).toBeVisible();
  await expect(page.getByPlaceholder("Email")).toBeVisible();
});

test("sign-in page has no console errors on load", async ({ page }) => {
  const errors: string[] = [];
  page.on("pageerror", (e) => errors.push(e.message));
  await page.goto("/sign-in");
  await expect(page.getByRole("heading", { name: "Sign in" })).toBeVisible();
  expect(errors).toEqual([]);
});
