/**
 * Feature switches that live in the JS bundle - changing one ships with an OTA
 * update, no store build.
 */

/**
 * Send "Forgot Password?" on the login screen to the email reset flow instead
 * of the old notify-an-admin popup.
 *
 * On in development. Turn it on for production only after the SES SMTP
 * credentials are set in the DigitalOcean console and a real reset email has
 * arrived end to end - until then the old popup is the flow that actually
 * works. Reset links inside emails open the new screen regardless.
 */
export const EMAIL_PASSWORD_RESET_ENABLED = __DEV__;
