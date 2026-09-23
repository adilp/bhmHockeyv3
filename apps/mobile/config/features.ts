/**
 * Feature switches that live in the JS bundle - changing one ships with an OTA
 * update, no store build.
 */

/**
 * Send "Forgot Password?" on the login screen to the email reset flow instead
 * of the old notify-an-admin popup.
 *
 * On everywhere: the SES SMTP credentials are set in the DigitalOcean console.
 * Set it back to false (and ship an OTA) to fall back to the popup if email
 * delivery breaks. Reset links inside emails open the new screen regardless.
 */
export const EMAIL_PASSWORD_RESET_ENABLED = true;
