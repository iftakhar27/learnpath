import 'dart:async';

import 'package:flutter/foundation.dart';
import 'package:flutter/widgets.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:intl/intl.dart' as intl;

import 'app_localizations_en.dart';
import 'app_localizations_hi.dart';

// ignore_for_file: type=lint

/// Callers can lookup localized strings with an instance of AppLocalizations
/// returned by `AppLocalizations.of(context)`.
///
/// Applications need to include `AppLocalizations.delegate()` in their app's
/// `localizationDelegates` list, and the locales they support in the app's
/// `supportedLocales` list. For example:
///
/// ```dart
/// import 'l10n/app_localizations.dart';
///
/// return MaterialApp(
///   localizationsDelegates: AppLocalizations.localizationsDelegates,
///   supportedLocales: AppLocalizations.supportedLocales,
///   home: MyApplicationHome(),
/// );
/// ```
///
/// ## Update pubspec.yaml
///
/// Please make sure to update your pubspec.yaml to include the following
/// packages:
///
/// ```yaml
/// dependencies:
///   # Internationalization support.
///   flutter_localizations:
///     sdk: flutter
///   intl: any # Use the pinned version from flutter_localizations
///
///   # Rest of dependencies
/// ```
///
/// ## iOS Applications
///
/// iOS applications define key application metadata, including supported
/// locales, in an Info.plist file that is built into the application bundle.
/// To configure the locales supported by your app, you’ll need to edit this
/// file.
///
/// First, open your project’s ios/Runner.xcworkspace Xcode workspace file.
/// Then, in the Project Navigator, open the Info.plist file under the Runner
/// project’s Runner folder.
///
/// Next, select the Information Property List item, select Add Item from the
/// Editor menu, then select Localizations from the pop-up menu.
///
/// Select and expand the newly-created Localizations item then, for each
/// locale your application supports, add a new item and select the locale
/// you wish to add from the pop-up menu in the Value field. This list should
/// be consistent with the languages listed in the AppLocalizations.supportedLocales
/// property.
abstract class AppLocalizations {
  AppLocalizations(String locale)
    : localeName = intl.Intl.canonicalizedLocale(locale.toString());

  final String localeName;

  static AppLocalizations of(BuildContext context) {
    return Localizations.of<AppLocalizations>(context, AppLocalizations)!;
  }

  static const LocalizationsDelegate<AppLocalizations> delegate =
      _AppLocalizationsDelegate();

  /// A list of this localizations delegate along with the default localizations
  /// delegates.
  ///
  /// Returns a list of localizations delegates containing this delegate along with
  /// GlobalMaterialLocalizations.delegate, GlobalCupertinoLocalizations.delegate,
  /// and GlobalWidgetsLocalizations.delegate.
  ///
  /// Additional delegates can be added by appending to this list in
  /// MaterialApp. This list does not have to be used at all if a custom list
  /// of delegates is preferred or required.
  static const List<LocalizationsDelegate<dynamic>> localizationsDelegates =
      <LocalizationsDelegate<dynamic>>[
        delegate,
        GlobalMaterialLocalizations.delegate,
        GlobalCupertinoLocalizations.delegate,
        GlobalWidgetsLocalizations.delegate,
      ];

  /// A list of this localizations delegate's supported locales.
  static const List<Locale> supportedLocales = <Locale>[
    Locale('en'),
    Locale('hi'),
  ];

  /// No description provided for @appTagline.
  ///
  /// In en, this message translates to:
  /// **'Prepare for NEET, JEE and Board exams'**
  String get appTagline;

  /// No description provided for @continueWithOtp.
  ///
  /// In en, this message translates to:
  /// **'Continue with mobile number'**
  String get continueWithOtp;

  /// No description provided for @continueWithEmail.
  ///
  /// In en, this message translates to:
  /// **'Sign in with email'**
  String get continueWithEmail;

  /// No description provided for @createAccount.
  ///
  /// In en, this message translates to:
  /// **'Create account'**
  String get createAccount;

  /// No description provided for @language.
  ///
  /// In en, this message translates to:
  /// **'Language'**
  String get language;

  /// No description provided for @english.
  ///
  /// In en, this message translates to:
  /// **'English'**
  String get english;

  /// No description provided for @hindi.
  ///
  /// In en, this message translates to:
  /// **'हिंदी'**
  String get hindi;

  /// No description provided for @email.
  ///
  /// In en, this message translates to:
  /// **'Email'**
  String get email;

  /// No description provided for @password.
  ///
  /// In en, this message translates to:
  /// **'Password'**
  String get password;

  /// No description provided for @fullName.
  ///
  /// In en, this message translates to:
  /// **'Full name'**
  String get fullName;

  /// No description provided for @signIn.
  ///
  /// In en, this message translates to:
  /// **'Sign in'**
  String get signIn;

  /// No description provided for @signOut.
  ///
  /// In en, this message translates to:
  /// **'Sign out'**
  String get signOut;

  /// No description provided for @newHereCreateAccount.
  ///
  /// In en, this message translates to:
  /// **'New here? Create an account'**
  String get newHereCreateAccount;

  /// No description provided for @haveAccountSignIn.
  ///
  /// In en, this message translates to:
  /// **'Already have an account? Sign in'**
  String get haveAccountSignIn;

  /// No description provided for @acceptTerms.
  ///
  /// In en, this message translates to:
  /// **'I agree to the Terms of Service and Privacy Policy'**
  String get acceptTerms;

  /// No description provided for @viewTerms.
  ///
  /// In en, this message translates to:
  /// **'Read terms and privacy policy'**
  String get viewTerms;

  /// No description provided for @mobileNumber.
  ///
  /// In en, this message translates to:
  /// **'Mobile number'**
  String get mobileNumber;

  /// No description provided for @sendOtp.
  ///
  /// In en, this message translates to:
  /// **'Send OTP'**
  String get sendOtp;

  /// No description provided for @enterOtp.
  ///
  /// In en, this message translates to:
  /// **'Enter the 6-digit OTP'**
  String get enterOtp;

  /// No description provided for @otpSentTo.
  ///
  /// In en, this message translates to:
  /// **'OTP sent to {phone}'**
  String otpSentTo(Object phone);

  /// No description provided for @verify.
  ///
  /// In en, this message translates to:
  /// **'Verify'**
  String get verify;

  /// No description provided for @resendIn.
  ///
  /// In en, this message translates to:
  /// **'Resend in {seconds}s'**
  String resendIn(Object seconds);

  /// No description provided for @resendOtp.
  ///
  /// In en, this message translates to:
  /// **'Resend OTP'**
  String get resendOtp;

  /// No description provided for @yourNameNewAccounts.
  ///
  /// In en, this message translates to:
  /// **'Your name (for new accounts)'**
  String get yourNameNewAccounts;

  /// No description provided for @changeNumber.
  ///
  /// In en, this message translates to:
  /// **'Change number'**
  String get changeNumber;

  /// No description provided for @fieldRequired.
  ///
  /// In en, this message translates to:
  /// **'This field is required'**
  String get fieldRequired;

  /// No description provided for @invalidEmail.
  ///
  /// In en, this message translates to:
  /// **'Enter a valid email'**
  String get invalidEmail;

  /// No description provided for @passwordTooShort.
  ///
  /// In en, this message translates to:
  /// **'Use at least 8 characters'**
  String get passwordTooShort;

  /// No description provided for @invalidPhone.
  ///
  /// In en, this message translates to:
  /// **'Enter a 10-digit mobile number'**
  String get invalidPhone;

  /// No description provided for @mustAcceptTerms.
  ///
  /// In en, this message translates to:
  /// **'Please accept the terms to continue'**
  String get mustAcceptTerms;

  /// No description provided for @errorNetwork.
  ///
  /// In en, this message translates to:
  /// **'No internet connection. Check your network and try again.'**
  String get errorNetwork;

  /// No description provided for @errorGeneric.
  ///
  /// In en, this message translates to:
  /// **'Something went wrong. Please try again.'**
  String get errorGeneric;

  /// No description provided for @retry.
  ///
  /// In en, this message translates to:
  /// **'Try again'**
  String get retry;

  /// No description provided for @serverUnreachable.
  ///
  /// In en, this message translates to:
  /// **'Can\'t reach the server'**
  String get serverUnreachable;

  /// No description provided for @sessionExpired.
  ///
  /// In en, this message translates to:
  /// **'Your session ended. Please sign in again.'**
  String get sessionExpired;

  /// No description provided for @onboardingTitle.
  ///
  /// In en, this message translates to:
  /// **'Set up your preparation'**
  String get onboardingTitle;

  /// No description provided for @chooseExam.
  ///
  /// In en, this message translates to:
  /// **'Which exam are you preparing for?'**
  String get chooseExam;

  /// No description provided for @targetYear.
  ///
  /// In en, this message translates to:
  /// **'Target year'**
  String get targetYear;

  /// No description provided for @yourClass.
  ///
  /// In en, this message translates to:
  /// **'Your class'**
  String get yourClass;

  /// No description provided for @classDropper.
  ///
  /// In en, this message translates to:
  /// **'Dropper'**
  String get classDropper;

  /// No description provided for @classLabel.
  ///
  /// In en, this message translates to:
  /// **'Class {level}'**
  String classLabel(Object level);

  /// No description provided for @board.
  ///
  /// In en, this message translates to:
  /// **'Board'**
  String get board;

  /// No description provided for @stateBoard.
  ///
  /// In en, this message translates to:
  /// **'State board'**
  String get stateBoard;

  /// No description provided for @dailyStudyTime.
  ///
  /// In en, this message translates to:
  /// **'Daily study time'**
  String get dailyStudyTime;

  /// No description provided for @minutesPerDay.
  ///
  /// In en, this message translates to:
  /// **'{minutes} minutes a day'**
  String minutesPerDay(Object minutes);

  /// No description provided for @under18.
  ///
  /// In en, this message translates to:
  /// **'I am under 18'**
  String get under18;

  /// No description provided for @guardianName.
  ///
  /// In en, this message translates to:
  /// **'Parent or guardian\'s name'**
  String get guardianName;

  /// No description provided for @guardianConsent.
  ///
  /// In en, this message translates to:
  /// **'My parent or guardian agrees to my use of this app'**
  String get guardianConsent;

  /// No description provided for @next.
  ///
  /// In en, this message translates to:
  /// **'Next'**
  String get next;

  /// No description provided for @back.
  ///
  /// In en, this message translates to:
  /// **'Back'**
  String get back;

  /// No description provided for @getStarted.
  ///
  /// In en, this message translates to:
  /// **'Get started'**
  String get getStarted;

  /// No description provided for @selectExamError.
  ///
  /// In en, this message translates to:
  /// **'Choose an exam to continue'**
  String get selectExamError;

  /// No description provided for @tabHome.
  ///
  /// In en, this message translates to:
  /// **'Home'**
  String get tabHome;

  /// No description provided for @tabLearn.
  ///
  /// In en, this message translates to:
  /// **'Learn'**
  String get tabLearn;

  /// No description provided for @tabPractice.
  ///
  /// In en, this message translates to:
  /// **'Practice'**
  String get tabPractice;

  /// No description provided for @tabTests.
  ///
  /// In en, this message translates to:
  /// **'Tests'**
  String get tabTests;

  /// No description provided for @tabProfile.
  ///
  /// In en, this message translates to:
  /// **'Profile'**
  String get tabProfile;

  /// No description provided for @greeting.
  ///
  /// In en, this message translates to:
  /// **'Hi, {name}'**
  String greeting(Object name);

  /// No description provided for @daysToExam.
  ///
  /// In en, this message translates to:
  /// **'{days, plural, =0{{exam} is today} =1{1 day to {exam}} other{{days} days to {exam}}}'**
  String daysToExam(num days, Object exam);

  /// No description provided for @streakDays.
  ///
  /// In en, this message translates to:
  /// **'{days, plural, =0{Start your streak today} =1{1-day streak} other{{days}-day streak}}'**
  String streakDays(num days);

  /// No description provided for @thisWeek.
  ///
  /// In en, this message translates to:
  /// **'This week'**
  String get thisWeek;

  /// No description provided for @questionsAnswered.
  ///
  /// In en, this message translates to:
  /// **'Questions answered'**
  String get questionsAnswered;

  /// No description provided for @lessonsCompleted.
  ///
  /// In en, this message translates to:
  /// **'Lessons completed'**
  String get lessonsCompleted;

  /// No description provided for @todaysPlan.
  ///
  /// In en, this message translates to:
  /// **'Today\'s plan'**
  String get todaysPlan;

  /// No description provided for @planBasis.
  ///
  /// In en, this message translates to:
  /// **'Suggested from your syllabus progress, recent accuracy and daily study time.'**
  String get planBasis;

  /// No description provided for @viewFullPlan.
  ///
  /// In en, this message translates to:
  /// **'View full plan'**
  String get viewFullPlan;

  /// No description provided for @planEmpty.
  ///
  /// In en, this message translates to:
  /// **'Nothing planned yet. Enroll in a course to get a daily plan.'**
  String get planEmpty;

  /// No description provided for @continueLearning.
  ///
  /// In en, this message translates to:
  /// **'Continue learning'**
  String get continueLearning;

  /// No description provided for @myCourses.
  ///
  /// In en, this message translates to:
  /// **'My courses'**
  String get myCourses;

  /// No description provided for @recentResults.
  ///
  /// In en, this message translates to:
  /// **'Recent results'**
  String get recentResults;

  /// No description provided for @weakTopics.
  ///
  /// In en, this message translates to:
  /// **'Topics to strengthen'**
  String get weakTopics;

  /// No description provided for @accuracyOn.
  ///
  /// In en, this message translates to:
  /// **'{accuracy}% accuracy on {count} questions'**
  String accuracyOn(Object accuracy, Object count);

  /// No description provided for @practise.
  ///
  /// In en, this message translates to:
  /// **'Practise'**
  String get practise;

  /// No description provided for @browseCourses.
  ///
  /// In en, this message translates to:
  /// **'Browse courses'**
  String get browseCourses;

  /// No description provided for @noCoursesYet.
  ///
  /// In en, this message translates to:
  /// **'You haven\'t enrolled in a course yet.'**
  String get noCoursesYet;

  /// No description provided for @minutesShort.
  ///
  /// In en, this message translates to:
  /// **'{minutes} min'**
  String minutesShort(Object minutes);

  /// No description provided for @percentComplete.
  ///
  /// In en, this message translates to:
  /// **'{percent}% complete'**
  String percentComplete(Object percent);

  /// No description provided for @searchHint.
  ///
  /// In en, this message translates to:
  /// **'Search courses, topics, tests'**
  String get searchHint;

  /// No description provided for @search.
  ///
  /// In en, this message translates to:
  /// **'Search'**
  String get search;

  /// No description provided for @noResults.
  ///
  /// In en, this message translates to:
  /// **'No results'**
  String get noResults;

  /// No description provided for @allCourses.
  ///
  /// In en, this message translates to:
  /// **'All'**
  String get allCourses;

  /// No description provided for @free.
  ///
  /// In en, this message translates to:
  /// **'Free'**
  String get free;

  /// No description provided for @paid.
  ///
  /// In en, this message translates to:
  /// **'Paid'**
  String get paid;

  /// No description provided for @enrolled.
  ///
  /// In en, this message translates to:
  /// **'Enrolled'**
  String get enrolled;

  /// No description provided for @coursesEmpty.
  ///
  /// In en, this message translates to:
  /// **'No courses match.'**
  String get coursesEmpty;

  /// No description provided for @enrollFree.
  ///
  /// In en, this message translates to:
  /// **'Enroll for free'**
  String get enrollFree;

  /// No description provided for @buyFor.
  ///
  /// In en, this message translates to:
  /// **'Buy for {price}'**
  String buyFor(Object price);

  /// No description provided for @accessDays.
  ///
  /// In en, this message translates to:
  /// **'{days} days access'**
  String accessDays(Object days);

  /// No description provided for @lifetimeAccess.
  ///
  /// In en, this message translates to:
  /// **'Lifetime access'**
  String get lifetimeAccess;

  /// No description provided for @youHaveAccess.
  ///
  /// In en, this message translates to:
  /// **'You have access to this course'**
  String get youHaveAccess;

  /// No description provided for @enrolledToast.
  ///
  /// In en, this message translates to:
  /// **'You\'re enrolled'**
  String get enrolledToast;

  /// No description provided for @syllabus.
  ///
  /// In en, this message translates to:
  /// **'Syllabus'**
  String get syllabus;

  /// No description provided for @topicsCount.
  ///
  /// In en, this message translates to:
  /// **'{count, plural, =1{1 topic} other{{count} topics}}'**
  String topicsCount(num count);

  /// No description provided for @objectives.
  ///
  /// In en, this message translates to:
  /// **'What you\'ll learn'**
  String get objectives;

  /// No description provided for @video.
  ///
  /// In en, this message translates to:
  /// **'Video'**
  String get video;

  /// No description provided for @notes.
  ///
  /// In en, this message translates to:
  /// **'Notes'**
  String get notes;

  /// No description provided for @freePreview.
  ///
  /// In en, this message translates to:
  /// **'Free preview'**
  String get freePreview;

  /// No description provided for @locked.
  ///
  /// In en, this message translates to:
  /// **'Locked'**
  String get locked;

  /// No description provided for @completed.
  ///
  /// In en, this message translates to:
  /// **'Completed'**
  String get completed;

  /// No description provided for @practiseTopic.
  ///
  /// In en, this message translates to:
  /// **'Practise this topic ({count} questions)'**
  String practiseTopic(Object count);

  /// No description provided for @noPracticeQuestions.
  ///
  /// In en, this message translates to:
  /// **'No practice questions for this topic yet.'**
  String get noPracticeQuestions;

  /// No description provided for @previousTopic.
  ///
  /// In en, this message translates to:
  /// **'Previous topic'**
  String get previousTopic;

  /// No description provided for @nextTopic.
  ///
  /// In en, this message translates to:
  /// **'Next topic'**
  String get nextTopic;

  /// No description provided for @unlockToContinue.
  ///
  /// In en, this message translates to:
  /// **'Buy the course to unlock this lesson.'**
  String get unlockToContinue;

  /// No description provided for @viewCourse.
  ///
  /// In en, this message translates to:
  /// **'View course'**
  String get viewCourse;

  /// No description provided for @videoUnavailable.
  ///
  /// In en, this message translates to:
  /// **'This video can\'t be played right now.'**
  String get videoUnavailable;

  /// No description provided for @playbackSpeed.
  ///
  /// In en, this message translates to:
  /// **'Playback speed'**
  String get playbackSpeed;

  /// No description provided for @resumedAt.
  ///
  /// In en, this message translates to:
  /// **'Resumed from {time}'**
  String resumedAt(Object time);

  /// No description provided for @openNotes.
  ///
  /// In en, this message translates to:
  /// **'Open notes'**
  String get openNotes;

  /// No description provided for @markAsRead.
  ///
  /// In en, this message translates to:
  /// **'Mark as read'**
  String get markAsRead;

  /// No description provided for @markedDone.
  ///
  /// In en, this message translates to:
  /// **'Marked as done'**
  String get markedDone;

  /// No description provided for @practiceTitle.
  ///
  /// In en, this message translates to:
  /// **'Practice'**
  String get practiceTitle;

  /// No description provided for @practiceMistakes.
  ///
  /// In en, this message translates to:
  /// **'Practise your mistakes'**
  String get practiceMistakes;

  /// No description provided for @practiceMistakesHint.
  ///
  /// In en, this message translates to:
  /// **'Questions you got wrong in practice and tests'**
  String get practiceMistakesHint;

  /// No description provided for @practiceBookmarked.
  ///
  /// In en, this message translates to:
  /// **'Bookmarked questions'**
  String get practiceBookmarked;

  /// No description provided for @practiceBookmarkedHint.
  ///
  /// In en, this message translates to:
  /// **'Questions you saved for revision'**
  String get practiceBookmarkedHint;

  /// No description provided for @practicePyq.
  ///
  /// In en, this message translates to:
  /// **'Previous-year questions'**
  String get practicePyq;

  /// No description provided for @practicePyqHint.
  ///
  /// In en, this message translates to:
  /// **'From past papers, where licensed'**
  String get practicePyqHint;

  /// No description provided for @practiceByTopic.
  ///
  /// In en, this message translates to:
  /// **'Practise by topic'**
  String get practiceByTopic;

  /// No description provided for @practiceByTopicHint.
  ///
  /// In en, this message translates to:
  /// **'Open any topic in Learn and tap Practise'**
  String get practiceByTopicHint;

  /// No description provided for @questionOf.
  ///
  /// In en, this message translates to:
  /// **'Question {current} of {total}'**
  String questionOf(Object current, Object total);

  /// No description provided for @checkAnswer.
  ///
  /// In en, this message translates to:
  /// **'Check answer'**
  String get checkAnswer;

  /// No description provided for @correct.
  ///
  /// In en, this message translates to:
  /// **'Correct'**
  String get correct;

  /// No description provided for @incorrect.
  ///
  /// In en, this message translates to:
  /// **'Incorrect'**
  String get incorrect;

  /// No description provided for @partiallyCorrect.
  ///
  /// In en, this message translates to:
  /// **'Partially correct'**
  String get partiallyCorrect;

  /// No description provided for @skipped.
  ///
  /// In en, this message translates to:
  /// **'Skipped'**
  String get skipped;

  /// No description provided for @explanation.
  ///
  /// In en, this message translates to:
  /// **'Explanation'**
  String get explanation;

  /// No description provided for @correctAnswer.
  ///
  /// In en, this message translates to:
  /// **'Correct answer: {answer}'**
  String correctAnswer(Object answer);

  /// No description provided for @yourAnswer.
  ///
  /// In en, this message translates to:
  /// **'Your answer: {answer}'**
  String yourAnswer(Object answer);

  /// No description provided for @notAnswered.
  ///
  /// In en, this message translates to:
  /// **'Not answered'**
  String get notAnswered;

  /// No description provided for @finish.
  ///
  /// In en, this message translates to:
  /// **'Finish'**
  String get finish;

  /// No description provided for @practiceSummary.
  ///
  /// In en, this message translates to:
  /// **'Practice summary'**
  String get practiceSummary;

  /// No description provided for @accuracy.
  ///
  /// In en, this message translates to:
  /// **'Accuracy'**
  String get accuracy;

  /// No description provided for @done.
  ///
  /// In en, this message translates to:
  /// **'Done'**
  String get done;

  /// No description provided for @enterNumber.
  ///
  /// In en, this message translates to:
  /// **'Enter your answer'**
  String get enterNumber;

  /// No description provided for @selectAllThatApply.
  ///
  /// In en, this message translates to:
  /// **'Select all that apply'**
  String get selectAllThatApply;

  /// No description provided for @hint.
  ///
  /// In en, this message translates to:
  /// **'Hint'**
  String get hint;

  /// No description provided for @bookmark.
  ///
  /// In en, this message translates to:
  /// **'Save for revision'**
  String get bookmark;

  /// No description provided for @bookmarked.
  ///
  /// In en, this message translates to:
  /// **'Saved for revision'**
  String get bookmarked;

  /// No description provided for @reportQuestion.
  ///
  /// In en, this message translates to:
  /// **'Report a problem'**
  String get reportQuestion;

  /// No description provided for @reportReasonWrong.
  ///
  /// In en, this message translates to:
  /// **'The answer key is wrong'**
  String get reportReasonWrong;

  /// No description provided for @reportReasonTypo.
  ///
  /// In en, this message translates to:
  /// **'Typo or formatting'**
  String get reportReasonTypo;

  /// No description provided for @reportReasonUnclear.
  ///
  /// In en, this message translates to:
  /// **'The question is unclear'**
  String get reportReasonUnclear;

  /// No description provided for @reportReasonOther.
  ///
  /// In en, this message translates to:
  /// **'Something else'**
  String get reportReasonOther;

  /// No description provided for @reportDetails.
  ///
  /// In en, this message translates to:
  /// **'Details (optional)'**
  String get reportDetails;

  /// No description provided for @send.
  ///
  /// In en, this message translates to:
  /// **'Send'**
  String get send;

  /// No description provided for @cancel.
  ///
  /// In en, this message translates to:
  /// **'Cancel'**
  String get cancel;

  /// No description provided for @reportThanks.
  ///
  /// In en, this message translates to:
  /// **'Thanks, our team will review it.'**
  String get reportThanks;

  /// No description provided for @noQuestionsForSelection.
  ///
  /// In en, this message translates to:
  /// **'No questions available here yet.'**
  String get noQuestionsForSelection;

  /// No description provided for @testsTitle.
  ///
  /// In en, this message translates to:
  /// **'Tests'**
  String get testsTitle;

  /// No description provided for @testsEmpty.
  ///
  /// In en, this message translates to:
  /// **'No tests available yet.'**
  String get testsEmpty;

  /// No description provided for @questionsCount.
  ///
  /// In en, this message translates to:
  /// **'{count, plural, =1{1 question} other{{count} questions}}'**
  String questionsCount(num count);

  /// No description provided for @marksCount.
  ///
  /// In en, this message translates to:
  /// **'{marks} marks'**
  String marksCount(Object marks);

  /// No description provided for @minutesCount.
  ///
  /// In en, this message translates to:
  /// **'{minutes} min'**
  String minutesCount(Object minutes);

  /// No description provided for @attemptsUsed.
  ///
  /// In en, this message translates to:
  /// **'Attempts: {used} of {max}'**
  String attemptsUsed(Object used, Object max);

  /// No description provided for @startTest.
  ///
  /// In en, this message translates to:
  /// **'Start test'**
  String get startTest;

  /// No description provided for @resumeTest.
  ///
  /// In en, this message translates to:
  /// **'Resume test'**
  String get resumeTest;

  /// No description provided for @retakeTest.
  ///
  /// In en, this message translates to:
  /// **'Retake'**
  String get retakeTest;

  /// No description provided for @viewResult.
  ///
  /// In en, this message translates to:
  /// **'View result'**
  String get viewResult;

  /// No description provided for @instructions.
  ///
  /// In en, this message translates to:
  /// **'Instructions'**
  String get instructions;

  /// No description provided for @sections.
  ///
  /// In en, this message translates to:
  /// **'Sections'**
  String get sections;

  /// No description provided for @negativeMarking.
  ///
  /// In en, this message translates to:
  /// **'−{marks} for each wrong answer'**
  String negativeMarking(Object marks);

  /// No description provided for @noNegativeMarking.
  ///
  /// In en, this message translates to:
  /// **'No negative marking'**
  String get noNegativeMarking;

  /// No description provided for @testRules.
  ///
  /// In en, this message translates to:
  /// **'The timer keeps running if you leave. Answers are saved as you go, and the test submits automatically when time runs out.'**
  String get testRules;

  /// No description provided for @timeLeft.
  ///
  /// In en, this message translates to:
  /// **'Time left'**
  String get timeLeft;

  /// No description provided for @markForReview.
  ///
  /// In en, this message translates to:
  /// **'Mark for review'**
  String get markForReview;

  /// No description provided for @unmarkReview.
  ///
  /// In en, this message translates to:
  /// **'Unmark review'**
  String get unmarkReview;

  /// No description provided for @clearResponse.
  ///
  /// In en, this message translates to:
  /// **'Clear'**
  String get clearResponse;

  /// No description provided for @previous.
  ///
  /// In en, this message translates to:
  /// **'Previous'**
  String get previous;

  /// No description provided for @saveAndNext.
  ///
  /// In en, this message translates to:
  /// **'Save & next'**
  String get saveAndNext;

  /// No description provided for @submitTest.
  ///
  /// In en, this message translates to:
  /// **'Submit test'**
  String get submitTest;

  /// No description provided for @questionPalette.
  ///
  /// In en, this message translates to:
  /// **'Questions'**
  String get questionPalette;

  /// No description provided for @paletteAnswered.
  ///
  /// In en, this message translates to:
  /// **'Answered'**
  String get paletteAnswered;

  /// No description provided for @paletteNotAnswered.
  ///
  /// In en, this message translates to:
  /// **'Not answered'**
  String get paletteNotAnswered;

  /// No description provided for @paletteMarked.
  ///
  /// In en, this message translates to:
  /// **'Marked for review'**
  String get paletteMarked;

  /// No description provided for @paletteNotVisited.
  ///
  /// In en, this message translates to:
  /// **'Not visited'**
  String get paletteNotVisited;

  /// No description provided for @submitConfirmTitle.
  ///
  /// In en, this message translates to:
  /// **'Submit the test?'**
  String get submitConfirmTitle;

  /// No description provided for @submitConfirmBody.
  ///
  /// In en, this message translates to:
  /// **'Answered: {answered}\nMarked for review: {marked}\nNot answered: {unanswered}\n\nYou can\'t change answers after submitting.'**
  String submitConfirmBody(Object answered, Object marked, Object unanswered);

  /// No description provided for @submit.
  ///
  /// In en, this message translates to:
  /// **'Submit'**
  String get submit;

  /// No description provided for @keepWorking.
  ///
  /// In en, this message translates to:
  /// **'Keep working'**
  String get keepWorking;

  /// No description provided for @leaveTestTitle.
  ///
  /// In en, this message translates to:
  /// **'Leave the test?'**
  String get leaveTestTitle;

  /// No description provided for @leaveTestBody.
  ///
  /// In en, this message translates to:
  /// **'Your answers are saved, but the timer keeps running. You can resume from the Tests tab.'**
  String get leaveTestBody;

  /// No description provided for @leave.
  ///
  /// In en, this message translates to:
  /// **'Leave'**
  String get leave;

  /// No description provided for @stay.
  ///
  /// In en, this message translates to:
  /// **'Stay'**
  String get stay;

  /// No description provided for @saving.
  ///
  /// In en, this message translates to:
  /// **'Saving…'**
  String get saving;

  /// No description provided for @saved.
  ///
  /// In en, this message translates to:
  /// **'Saved'**
  String get saved;

  /// No description provided for @offlineWillRetry.
  ///
  /// In en, this message translates to:
  /// **'Offline. Answers will be saved when you\'re back online.'**
  String get offlineWillRetry;

  /// No description provided for @timeUp.
  ///
  /// In en, this message translates to:
  /// **'Time\'s up. Submitting your test…'**
  String get timeUp;

  /// No description provided for @resultTitle.
  ///
  /// In en, this message translates to:
  /// **'Result'**
  String get resultTitle;

  /// No description provided for @score.
  ///
  /// In en, this message translates to:
  /// **'Score'**
  String get score;

  /// No description provided for @scoreOf.
  ///
  /// In en, this message translates to:
  /// **'{score} / {max}'**
  String scoreOf(Object score, Object max);

  /// No description provided for @timeTaken.
  ///
  /// In en, this message translates to:
  /// **'Time taken'**
  String get timeTaken;

  /// No description provided for @percentileLine.
  ///
  /// In en, this message translates to:
  /// **'Better than {percent}% of first attempts on this platform'**
  String percentileLine(Object percent);

  /// No description provided for @notOfficialRank.
  ///
  /// In en, this message translates to:
  /// **'Not an official rank.'**
  String get notOfficialRank;

  /// No description provided for @subjectWise.
  ///
  /// In en, this message translates to:
  /// **'Subject-wise'**
  String get subjectWise;

  /// No description provided for @chapterWise.
  ///
  /// In en, this message translates to:
  /// **'Chapter-wise'**
  String get chapterWise;

  /// No description provided for @recommendations.
  ///
  /// In en, this message translates to:
  /// **'What to do next'**
  String get recommendations;

  /// No description provided for @reviewAnswers.
  ///
  /// In en, this message translates to:
  /// **'Review answers'**
  String get reviewAnswers;

  /// No description provided for @backToTests.
  ///
  /// In en, this message translates to:
  /// **'Back to tests'**
  String get backToTests;

  /// No description provided for @filterAll.
  ///
  /// In en, this message translates to:
  /// **'All'**
  String get filterAll;

  /// No description provided for @filterIncorrect.
  ///
  /// In en, this message translates to:
  /// **'Incorrect'**
  String get filterIncorrect;

  /// No description provided for @filterSkipped.
  ///
  /// In en, this message translates to:
  /// **'Skipped'**
  String get filterSkipped;

  /// No description provided for @marksAwarded.
  ///
  /// In en, this message translates to:
  /// **'{marks} marks'**
  String marksAwarded(Object marks);

  /// No description provided for @autoSubmitted.
  ///
  /// In en, this message translates to:
  /// **'Submitted automatically when time ran out'**
  String get autoSubmitted;

  /// No description provided for @attemptNumber.
  ///
  /// In en, this message translates to:
  /// **'Attempt {number}'**
  String attemptNumber(Object number);

  /// No description provided for @planTitle.
  ///
  /// In en, this message translates to:
  /// **'Study plan'**
  String get planTitle;

  /// No description provided for @regeneratePlan.
  ///
  /// In en, this message translates to:
  /// **'Refresh plan'**
  String get regeneratePlan;

  /// No description provided for @skip.
  ///
  /// In en, this message translates to:
  /// **'Skip'**
  String get skip;

  /// No description provided for @plannedMinutes.
  ///
  /// In en, this message translates to:
  /// **'{planned} of {daily} min planned'**
  String plannedMinutes(Object planned, Object daily);

  /// No description provided for @lastSevenDays.
  ///
  /// In en, this message translates to:
  /// **'Last 7 days'**
  String get lastSevenDays;

  /// No description provided for @tasksDone.
  ///
  /// In en, this message translates to:
  /// **'{done}/{total} done'**
  String tasksDone(Object done, Object total);

  /// No description provided for @profileTitle.
  ///
  /// In en, this message translates to:
  /// **'Profile'**
  String get profileTitle;

  /// No description provided for @preparingFor.
  ///
  /// In en, this message translates to:
  /// **'Preparing for {exam} {year}'**
  String preparingFor(Object exam, Object year);

  /// No description provided for @editPreferences.
  ///
  /// In en, this message translates to:
  /// **'Study preferences'**
  String get editPreferences;

  /// No description provided for @myProgress.
  ///
  /// In en, this message translates to:
  /// **'My progress'**
  String get myProgress;

  /// No description provided for @myPurchases.
  ///
  /// In en, this message translates to:
  /// **'My purchases'**
  String get myPurchases;

  /// No description provided for @bookmarksTitle.
  ///
  /// In en, this message translates to:
  /// **'Saved for revision'**
  String get bookmarksTitle;

  /// No description provided for @notificationsTitle.
  ///
  /// In en, this message translates to:
  /// **'Notifications'**
  String get notificationsTitle;

  /// No description provided for @helpSupport.
  ///
  /// In en, this message translates to:
  /// **'Help & support'**
  String get helpSupport;

  /// No description provided for @privacyPolicy.
  ///
  /// In en, this message translates to:
  /// **'Privacy policy'**
  String get privacyPolicy;

  /// No description provided for @deleteAccount.
  ///
  /// In en, this message translates to:
  /// **'Delete my account'**
  String get deleteAccount;

  /// No description provided for @deleteAccountBody.
  ///
  /// In en, this message translates to:
  /// **'We\'ll delete your account and personal data after any open orders or refunds are settled. You\'ll be signed out now.'**
  String get deleteAccountBody;

  /// No description provided for @delete.
  ///
  /// In en, this message translates to:
  /// **'Delete'**
  String get delete;

  /// No description provided for @save.
  ///
  /// In en, this message translates to:
  /// **'Save'**
  String get save;

  /// No description provided for @savedToast.
  ///
  /// In en, this message translates to:
  /// **'Saved'**
  String get savedToast;

  /// No description provided for @notificationsEmpty.
  ///
  /// In en, this message translates to:
  /// **'No notifications yet.'**
  String get notificationsEmpty;

  /// No description provided for @markAllRead.
  ///
  /// In en, this message translates to:
  /// **'Mark all as read'**
  String get markAllRead;

  /// No description provided for @bookmarksEmpty.
  ///
  /// In en, this message translates to:
  /// **'Nothing saved yet. Tap the bookmark on a question to save it.'**
  String get bookmarksEmpty;

  /// No description provided for @remove.
  ///
  /// In en, this message translates to:
  /// **'Remove'**
  String get remove;

  /// No description provided for @ordersEmpty.
  ///
  /// In en, this message translates to:
  /// **'No purchases yet.'**
  String get ordersEmpty;

  /// No description provided for @orderPaid.
  ///
  /// In en, this message translates to:
  /// **'Paid'**
  String get orderPaid;

  /// No description provided for @orderPending.
  ///
  /// In en, this message translates to:
  /// **'Pending'**
  String get orderPending;

  /// No description provided for @orderFailed.
  ///
  /// In en, this message translates to:
  /// **'Failed'**
  String get orderFailed;

  /// No description provided for @orderRefunded.
  ///
  /// In en, this message translates to:
  /// **'Refunded'**
  String get orderRefunded;

  /// No description provided for @ticketsEmpty.
  ///
  /// In en, this message translates to:
  /// **'You haven\'t contacted support yet.'**
  String get ticketsEmpty;

  /// No description provided for @newTicket.
  ///
  /// In en, this message translates to:
  /// **'Contact support'**
  String get newTicket;

  /// No description provided for @ticketCategory.
  ///
  /// In en, this message translates to:
  /// **'Topic'**
  String get ticketCategory;

  /// No description provided for @categoryPayment.
  ///
  /// In en, this message translates to:
  /// **'Payment'**
  String get categoryPayment;

  /// No description provided for @categoryContent.
  ///
  /// In en, this message translates to:
  /// **'Course content'**
  String get categoryContent;

  /// No description provided for @categoryTechnical.
  ///
  /// In en, this message translates to:
  /// **'App problem'**
  String get categoryTechnical;

  /// No description provided for @categoryAccount.
  ///
  /// In en, this message translates to:
  /// **'Account'**
  String get categoryAccount;

  /// No description provided for @categoryOther.
  ///
  /// In en, this message translates to:
  /// **'Other'**
  String get categoryOther;

  /// No description provided for @ticketSubject.
  ///
  /// In en, this message translates to:
  /// **'Subject'**
  String get ticketSubject;

  /// No description provided for @ticketMessage.
  ///
  /// In en, this message translates to:
  /// **'How can we help?'**
  String get ticketMessage;

  /// No description provided for @ticketSent.
  ///
  /// In en, this message translates to:
  /// **'Message sent. We\'ll reply here and notify you.'**
  String get ticketSent;

  /// No description provided for @supportReply.
  ///
  /// In en, this message translates to:
  /// **'Reply from support'**
  String get supportReply;

  /// No description provided for @progressTitle.
  ///
  /// In en, this message translates to:
  /// **'My progress'**
  String get progressTitle;

  /// No description provided for @testsTaken.
  ///
  /// In en, this message translates to:
  /// **'Tests taken'**
  String get testsTaken;

  /// No description provided for @averageScore.
  ///
  /// In en, this message translates to:
  /// **'Average score'**
  String get averageScore;

  /// No description provided for @practiceAccuracy.
  ///
  /// In en, this message translates to:
  /// **'Practice accuracy'**
  String get practiceAccuracy;

  /// No description provided for @scoreTrend.
  ///
  /// In en, this message translates to:
  /// **'Score trend'**
  String get scoreTrend;

  /// No description provided for @noTestsYet.
  ///
  /// In en, this message translates to:
  /// **'Take a test to see your progress here.'**
  String get noTestsYet;

  /// No description provided for @purchaseOnMobileOnly.
  ///
  /// In en, this message translates to:
  /// **'Purchases are available in the Android and iOS app.'**
  String get purchaseOnMobileOnly;

  /// No description provided for @paymentSuccess.
  ///
  /// In en, this message translates to:
  /// **'Payment successful. The course is unlocked.'**
  String get paymentSuccess;

  /// No description provided for @paymentFailed.
  ///
  /// In en, this message translates to:
  /// **'Payment didn\'t complete. No money was taken, or it will be refunded automatically.'**
  String get paymentFailed;

  /// No description provided for @paymentVerifying.
  ///
  /// In en, this message translates to:
  /// **'Confirming your payment…'**
  String get paymentVerifying;

  /// No description provided for @purchaseRequired.
  ///
  /// In en, this message translates to:
  /// **'Buy this course to unlock it.'**
  String get purchaseRequired;

  /// No description provided for @loading.
  ///
  /// In en, this message translates to:
  /// **'Loading…'**
  String get loading;

  /// No description provided for @emptyGeneric.
  ///
  /// In en, this message translates to:
  /// **'Nothing here yet.'**
  String get emptyGeneric;

  /// No description provided for @play.
  ///
  /// In en, this message translates to:
  /// **'Play'**
  String get play;

  /// No description provided for @pause.
  ///
  /// In en, this message translates to:
  /// **'Pause'**
  String get pause;

  /// No description provided for @fullScreen.
  ///
  /// In en, this message translates to:
  /// **'Full screen'**
  String get fullScreen;

  /// No description provided for @exitFullScreen.
  ///
  /// In en, this message translates to:
  /// **'Exit full screen'**
  String get exitFullScreen;

  /// No description provided for @sessionActiveTitle.
  ///
  /// In en, this message translates to:
  /// **'Already signed in on another device'**
  String get sessionActiveTitle;

  /// No description provided for @sessionActiveBody.
  ///
  /// In en, this message translates to:
  /// **'This account is signed in on {device} (last active {time}). Continue here? That device will be signed out.'**
  String sessionActiveBody(String device, String time);

  /// No description provided for @continueHere.
  ///
  /// In en, this message translates to:
  /// **'Continue here'**
  String get continueHere;

  /// No description provided for @sessionReplaced.
  ///
  /// In en, this message translates to:
  /// **'You were signed out because your account was signed in on another device.'**
  String get sessionReplaced;

  /// No description provided for @timeJustNow.
  ///
  /// In en, this message translates to:
  /// **'just now'**
  String get timeJustNow;

  /// No description provided for @timeMinutesAgo.
  ///
  /// In en, this message translates to:
  /// **'{count, plural, =1{1 minute ago} other{{count} minutes ago}}'**
  String timeMinutesAgo(int count);

  /// No description provided for @timeHoursAgo.
  ///
  /// In en, this message translates to:
  /// **'{count, plural, =1{1 hour ago} other{{count} hours ago}}'**
  String timeHoursAgo(int count);

  /// No description provided for @timeDaysAgo.
  ///
  /// In en, this message translates to:
  /// **'{count, plural, =1{1 day ago} other{{count} days ago}}'**
  String timeDaysAgo(int count);

  /// No description provided for @doubtsTitle.
  ///
  /// In en, this message translates to:
  /// **'My doubts'**
  String get doubtsTitle;

  /// No description provided for @askDoubt.
  ///
  /// In en, this message translates to:
  /// **'Ask a doubt'**
  String get askDoubt;

  /// No description provided for @doubtsIntro.
  ///
  /// In en, this message translates to:
  /// **'Stuck on something? Ask a teacher. You\'ll get a notification when they reply.'**
  String get doubtsIntro;

  /// No description provided for @yourDoubt.
  ///
  /// In en, this message translates to:
  /// **'Your doubt'**
  String get yourDoubt;

  /// No description provided for @doubtHint.
  ///
  /// In en, this message translates to:
  /// **'Describe where you\'re stuck: the question, the step, or what isn\'t clear.'**
  String get doubtHint;

  /// No description provided for @doubtAbout.
  ///
  /// In en, this message translates to:
  /// **'About: {title}'**
  String doubtAbout(Object title);

  /// No description provided for @doubtGeneral.
  ///
  /// In en, this message translates to:
  /// **'General doubt'**
  String get doubtGeneral;

  /// No description provided for @doubtSent.
  ///
  /// In en, this message translates to:
  /// **'Doubt sent. A teacher will reply soon.'**
  String get doubtSent;

  /// No description provided for @doubtTooShort.
  ///
  /// In en, this message translates to:
  /// **'Write at least 5 characters.'**
  String get doubtTooShort;

  /// No description provided for @doubtsEmpty.
  ///
  /// In en, this message translates to:
  /// **'You haven\'t asked any doubts yet.'**
  String get doubtsEmpty;

  /// No description provided for @doubtStatusOpen.
  ///
  /// In en, this message translates to:
  /// **'Waiting for a teacher'**
  String get doubtStatusOpen;

  /// No description provided for @doubtStatusAnswered.
  ///
  /// In en, this message translates to:
  /// **'Answered'**
  String get doubtStatusAnswered;

  /// No description provided for @doubtStatusResolved.
  ///
  /// In en, this message translates to:
  /// **'Resolved'**
  String get doubtStatusResolved;

  /// No description provided for @doubtReplies.
  ///
  /// In en, this message translates to:
  /// **'{count, plural, =0{No replies yet} =1{1 reply} other{{count} replies}}'**
  String doubtReplies(int count);

  /// No description provided for @doubtYou.
  ///
  /// In en, this message translates to:
  /// **'You'**
  String get doubtYou;

  /// No description provided for @doubtTeacher.
  ///
  /// In en, this message translates to:
  /// **'Teacher'**
  String get doubtTeacher;

  /// No description provided for @doubtFollowUp.
  ///
  /// In en, this message translates to:
  /// **'Ask a follow-up'**
  String get doubtFollowUp;

  /// No description provided for @doubtMarkResolved.
  ///
  /// In en, this message translates to:
  /// **'My doubt is solved'**
  String get doubtMarkResolved;

  /// No description provided for @doubtResolvedHint.
  ///
  /// In en, this message translates to:
  /// **'Still unclear? Ask a follow-up and a teacher will look again.'**
  String get doubtResolvedHint;

  /// No description provided for @topicQuiz.
  ///
  /// In en, this message translates to:
  /// **'Topic quiz'**
  String get topicQuiz;

  /// No description provided for @topicQuizInfo.
  ///
  /// In en, this message translates to:
  /// **'{count, plural, =1{1 question} other{{count} questions}} · pass mark {percent}%'**
  String topicQuizInfo(int count, Object percent);

  /// No description provided for @topicQuizCovered.
  ///
  /// In en, this message translates to:
  /// **'You\'ve covered this topic. Check your understanding with a short quiz.'**
  String get topicQuizCovered;

  /// No description provided for @topicQuizNotCovered.
  ///
  /// In en, this message translates to:
  /// **'Finish the lessons first, or test yourself now.'**
  String get topicQuizNotCovered;

  /// No description provided for @topicDonePrompt.
  ///
  /// In en, this message translates to:
  /// **'Topic covered! Take a quick {count, plural, =1{1-question} other{{count}-question}} quiz.'**
  String topicDonePrompt(int count);

  /// No description provided for @startQuiz.
  ///
  /// In en, this message translates to:
  /// **'Start quiz'**
  String get startQuiz;

  /// No description provided for @retakeQuiz.
  ///
  /// In en, this message translates to:
  /// **'Retake quiz'**
  String get retakeQuiz;

  /// No description provided for @quizBest.
  ///
  /// In en, this message translates to:
  /// **'Best score {percent}%'**
  String quizBest(Object percent);

  /// No description provided for @quizPassed.
  ///
  /// In en, this message translates to:
  /// **'Passed'**
  String get quizPassed;

  /// No description provided for @quizNotPassed.
  ///
  /// In en, this message translates to:
  /// **'Not passed yet'**
  String get quizNotPassed;

  /// No description provided for @submitQuiz.
  ///
  /// In en, this message translates to:
  /// **'Submit quiz'**
  String get submitQuiz;

  /// No description provided for @quizUnanswered.
  ///
  /// In en, this message translates to:
  /// **'{count, plural, =1{1 question is unanswered.} other{{count} questions are unanswered.}} Submit anyway?'**
  String quizUnanswered(int count);

  /// No description provided for @quizResult.
  ///
  /// In en, this message translates to:
  /// **'Quiz result'**
  String get quizResult;

  /// No description provided for @quizPassedBody.
  ///
  /// In en, this message translates to:
  /// **'Well done! You passed this topic\'s quiz.'**
  String get quizPassedBody;

  /// No description provided for @quizFailedBody.
  ///
  /// In en, this message translates to:
  /// **'You need {percent}% to pass. Revise the lessons and try again.'**
  String quizFailedBody(Object percent);

  /// No description provided for @backToTopic.
  ///
  /// In en, this message translates to:
  /// **'Back to topic'**
  String get backToTopic;

  /// No description provided for @buyOnWebsite.
  ///
  /// In en, this message translates to:
  /// **'Buy on website · {price}'**
  String buyOnWebsite(Object price);

  /// No description provided for @buyOnWebsiteHint.
  ///
  /// In en, this message translates to:
  /// **'Opens our website to pay securely. Come back to the app afterwards: the course unlocks by itself.'**
  String get buyOnWebsiteHint;

  /// No description provided for @notForSaleInApp.
  ///
  /// In en, this message translates to:
  /// **'This course can\'t be bought in the app.'**
  String get notForSaleInApp;

  /// No description provided for @changePassword.
  ///
  /// In en, this message translates to:
  /// **'Change password'**
  String get changePassword;

  /// No description provided for @currentPassword.
  ///
  /// In en, this message translates to:
  /// **'Current password'**
  String get currentPassword;

  /// No description provided for @newPassword.
  ///
  /// In en, this message translates to:
  /// **'New password'**
  String get newPassword;

  /// No description provided for @confirmNewPassword.
  ///
  /// In en, this message translates to:
  /// **'Confirm new password'**
  String get confirmNewPassword;

  /// No description provided for @passwordsDontMatch.
  ///
  /// In en, this message translates to:
  /// **'The passwords don\'t match'**
  String get passwordsDontMatch;

  /// No description provided for @passwordChangedToast.
  ///
  /// In en, this message translates to:
  /// **'Password changed. Other devices were signed out.'**
  String get passwordChangedToast;
}

class _AppLocalizationsDelegate
    extends LocalizationsDelegate<AppLocalizations> {
  const _AppLocalizationsDelegate();

  @override
  Future<AppLocalizations> load(Locale locale) {
    return SynchronousFuture<AppLocalizations>(lookupAppLocalizations(locale));
  }

  @override
  bool isSupported(Locale locale) =>
      <String>['en', 'hi'].contains(locale.languageCode);

  @override
  bool shouldReload(_AppLocalizationsDelegate old) => false;
}

AppLocalizations lookupAppLocalizations(Locale locale) {
  // Lookup logic when only language code is specified.
  switch (locale.languageCode) {
    case 'en':
      return AppLocalizationsEn();
    case 'hi':
      return AppLocalizationsHi();
  }

  throw FlutterError(
    'AppLocalizations.delegate failed to load unsupported locale "$locale". This is likely '
    'an issue with the localizations generation tool. Please file an issue '
    'on GitHub with a reproducible sample app and the gen-l10n configuration '
    'that was used.',
  );
}
