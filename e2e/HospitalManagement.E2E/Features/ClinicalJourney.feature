@R-001 @R-002 @R-010 @R-011 @A-001
Feature: A visit from booking to medical record
  A patient books an appointment, the doctor records a diagnosis, the patient reads their own record and nothing
  of another patient. Every step uses a real Keycloak token from a browser sign-in against the deployed API.

  Background:
    Given the administrator has linked "patient.user" to a new patient record
    And the administrator has linked "doctor.user" to a new doctor profile
    And another patient has a medical record written by that doctor

  @F-03.1 @F-10.1
  Scenario: A patient books an appointment and sees only their own appointments
    Given "patient.user" is signed in
    When the patient books an appointment with the linked doctor
    Then the appointment is in the patient's list with status "Scheduled"
    And every appointment in the patient's list belongs to the patient

  @F-03.8
  Scenario: A patient cannot book for another patient
    Given "patient.user" is signed in
    When the patient tries to book an appointment for the other patient
    Then the API refuses it with 403

  @F-08.1 @F-08.2 @F-10.2
  Scenario: The doctor's diagnosis reaches the patient, another patient's record stays hidden
    Given "doctor.user" is signed in
    When the doctor records the diagnosis "Seasonal influenza" for the linked patient
    Then the record's author is the linked doctor profile
    Given "patient.user" is signed in
    Then the patient sees the diagnosis "Seasonal influenza" in their medical records
    And opening the other patient's medical record is refused with 403
    And opening a medical record that does not exist is refused with 403

  @F-09.1 @F-09.4 @F-09.5
  Scenario: An amendment is kept as a new version that clinical staff can read
    Given "doctor.user" is signed in
    And the doctor has recorded the diagnosis "Common cold" for the linked patient
    When the doctor amends the diagnosis to "Seasonal influenza"
    Given "nurse.user" is signed in
    Then the record's history shows "Common cold" then "Seasonal influenza"
    Given "patient.user" is signed in
    Then reading the record's history is refused with 403
