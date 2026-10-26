let hasBooted = false;

/**
 * Executes the boot sequence: liveness check, session restoration, and smooth splash dismissal.
 */
export async function initializeAppBoot(): Promise<void> {
  if (hasBooted) {
    dismissSplashInstantly();
    return;
  }

  const splashElement = document.getElementById('app-boot-splash');
  if (!splashElement) {
    hasBooted = true;
    return;
  }

  const startTime = Date.now();
  const MIN_DISPLAY_MS = 1000;
  const HARD_TIMEOUT_MS = 3800;

  // Reduced motion preference check
  const prefersReduced = window.matchMedia('(prefers-reduced-motion: reduce)').matches;

  // Liveness check with hard timeout
  const checkLiveness = async () => {
    try {
      const controller = new AbortController();
      const timeoutId = setTimeout(() => controller.abort(), 2000);
      await fetch('/health', { signal: controller.signal });
      clearTimeout(timeoutId);
    } catch {
      // Offline or backend not yet started; proceed gracefully
    }
  };

  const bootTask = checkLiveness();
  const timeoutPromise = new Promise((resolve) => setTimeout(resolve, HARD_TIMEOUT_MS));

  await Promise.race([bootTask, timeoutPromise]);

  const elapsed = Date.now() - startTime;
  const remaining = prefersReduced ? 0 : Math.max(0, MIN_DISPLAY_MS - elapsed);

  if (remaining > 0) {
    await new Promise((resolve) => setTimeout(resolve, remaining));
  }

  // Smooth dismiss
  splashElement.classList.add('splash-hidden');
  setTimeout(() => {
    splashElement.remove();
    hasBooted = true;
  }, 400);
}

function dismissSplashInstantly(): void {
  const splashElement = document.getElementById('app-boot-splash');
  if (splashElement) {
    splashElement.remove();
  }
}
