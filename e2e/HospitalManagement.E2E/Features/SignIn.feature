@R-006 @R-015 @F-13
Feature: Signing in to the hospital web application
  Staff and patients sign in on the Keycloak login page of the hospital realm (Authorization Code + PKCE).
  The application shows only to users with a hospital role, and signing out ends the Keycloak session.

  @F-13.1
  Scenario Outline: Each hospital role signs in and sees their own name
    Given I open the web application
    Then I am asked to sign in on the Keycloak page
    When I sign in as "<user>"
    Then the application shows "<name>" as the signed-in user

    Examples:
      | user         | name        |
      | admin.user   | Alex Admin  |
      | doctor.user  | Dana Doctor |
      | nurse.user   | Noel Nurse  |
      | patient.user | Pat Patient |

  @R-043
  Scenario: The sign-in page offers no password reset while no mail server is configured
    Given I open the web application
    Then I am asked to sign in on the Keycloak page
    And the sign-in page offers no "Forgot Password" link

  @F-01.6 @R-024
  Scenario: An account without a hospital role is not let in
    Given a Keycloak account without a hospital role
    When that account signs in to the web application
    Then the application shows "No access"
    And the API refuses that account's token with 403

  @F-13.3
  Scenario: Signing out ends the session
    Given I am signed in as "nurse.user"
    When I sign out
    Then I am asked to sign in on the Keycloak page
    When I open the web application
    Then I am asked to sign in on the Keycloak page
