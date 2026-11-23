Feature: Editing the catalogue in the web application keeps what the form does not change
  The administrator edits doctors, patients and services through the web application. The API's PUT replaces
  every field, so the form must send back what it does not show. Every check compares the web application with
  what the API stored. Data is synthetic and created for each scenario (BUSINESS_FLOWS §4).

  Background:
    Given the administrator works in the web application

  @R-059
  Scenario: Renaming a doctor keeps their birthday, department and services
    Given a doctor born on "1980-01-02" with a department and a service
    When the administrator renames the doctor in the web application
    Then the web application saves the change with PUT
    And the list shows the doctor's new name
    And the doctor's birthday, department and services are unchanged

  @R-059
  Scenario: Renaming a patient keeps their email address
    Given a patient with an email address
    When the administrator renames the patient in the web application
    Then the web application saves the change with PUT
    And the list shows the patient's new name
    And the patient's email address, phone and service are unchanged

  @R-059
  Scenario: Changing a service's description keeps its department
    Given a service with a department
    When the administrator changes the service's description in the web application
    Then the web application saves the change with PUT
    And the service's new description is stored and its department is unchanged

  @R-060
  Scenario Outline: A birthday picked in the web application is saved as that day
    Given a service with a department
    When the administrator adds a doctor born on "1980-01-02" in the web application
    Then the web application saves the new record
    And the new doctor's stored birthday is "1980-01-02"
    And editing the new doctor shows the birthday "1980-01-02"

    @timezone:Europe/Sarajevo
    Examples: east of UTC
      | zone            |
      | Europe/Sarajevo |

    @timezone:America/New_York
    Examples: west of UTC
      | zone             |
      | America/New_York |
