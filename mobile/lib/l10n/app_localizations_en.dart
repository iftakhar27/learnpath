// ignore: unused_import
import 'package:intl/intl.dart' as intl;

import 'app_localizations.dart';

// ignore_for_file: type=lint

/// The translations for English (`en`).
class AppLocalizationsEn extends AppLocalizations {
  AppLocalizationsEn([String locale = 'en']) : super(locale);

  @override
  String get appTagline => 'Prepare for NEET, JEE and Board exams';

  @override
  String get continueWithOtp => 'Continue with mobile number';

  @override
  String get continueWithEmail => 'Sign in with email';

  @override
  String get createAccount => 'Create account';

  @override
  String get language => 'Language';

  @override
  String get english => 'English';

  @override
  String get hindi => 'हिंदी';

  @override
  String get email => 'Email';

  @override
  String get password => 'Password';

  @override
  String get fullName => 'Full name';

  @override
  String get signIn => 'Sign in';

  @override
  String get signOut => 'Sign out';

  @override
  String get newHereCreateAccount => 'New here? Create an account';

  @override
  String get haveAccountSignIn => 'Already have an account? Sign in';

  @override
  String get acceptTerms =>
      'I agree to the Terms of Service and Privacy Policy';

  @override
  String get viewTerms => 'Read terms and privacy policy';

  @override
  String get mobileNumber => 'Mobile number';

  @override
  String get sendOtp => 'Send OTP';

  @override
  String get enterOtp => 'Enter the 6-digit OTP';

  @override
  String otpSentTo(Object phone) {
    return 'OTP sent to $phone';
  }

  @override
  String get verify => 'Verify';

  @override
  String resendIn(Object seconds) {
    return 'Resend in ${seconds}s';
  }

  @override
  String get resendOtp => 'Resend OTP';

  @override
  String get yourNameNewAccounts => 'Your name (for new accounts)';

  @override
  String get changeNumber => 'Change number';

  @override
  String get fieldRequired => 'This field is required';

  @override
  String get invalidEmail => 'Enter a valid email';

  @override
  String get passwordTooShort => 'Use at least 8 characters';

  @override
  String get invalidPhone => 'Enter a 10-digit mobile number';

  @override
  String get mustAcceptTerms => 'Please accept the terms to continue';

  @override
  String get errorNetwork =>
      'No internet connection. Check your network and try again.';

  @override
  String get errorGeneric => 'Something went wrong. Please try again.';

  @override
  String get retry => 'Try again';

  @override
  String get serverUnreachable => 'Can\'t reach the server';

  @override
  String get sessionExpired => 'Your session ended. Please sign in again.';

  @override
  String get onboardingTitle => 'Set up your preparation';

  @override
  String get chooseExam => 'Which exam are you preparing for?';

  @override
  String get targetYear => 'Target year';

  @override
  String get yourClass => 'Your class';

  @override
  String get classDropper => 'Dropper';

  @override
  String classLabel(Object level) {
    return 'Class $level';
  }

  @override
  String get board => 'Board';

  @override
  String get stateBoard => 'State board';

  @override
  String get dailyStudyTime => 'Daily study time';

  @override
  String minutesPerDay(Object minutes) {
    return '$minutes minutes a day';
  }

  @override
  String get under18 => 'I am under 18';

  @override
  String get guardianName => 'Parent or guardian\'s name';

  @override
  String get guardianConsent =>
      'My parent or guardian agrees to my use of this app';

  @override
  String get next => 'Next';

  @override
  String get back => 'Back';

  @override
  String get getStarted => 'Get started';

  @override
  String get selectExamError => 'Choose an exam to continue';

  @override
  String get tabHome => 'Home';

  @override
  String get tabLearn => 'Learn';

  @override
  String get tabPractice => 'Practice';

  @override
  String get tabTests => 'Tests';

  @override
  String get tabProfile => 'Profile';

  @override
  String greeting(Object name) {
    return 'Hi, $name';
  }

  @override
  String daysToExam(num days, Object exam) {
    String _temp0 = intl.Intl.pluralLogic(
      days,
      locale: localeName,
      other: '$days days to $exam',
      one: '1 day to $exam',
      zero: '$exam is today',
    );
    return '$_temp0';
  }

  @override
  String streakDays(num days) {
    String _temp0 = intl.Intl.pluralLogic(
      days,
      locale: localeName,
      other: '$days-day streak',
      one: '1-day streak',
      zero: 'Start your streak today',
    );
    return '$_temp0';
  }

  @override
  String get thisWeek => 'This week';

  @override
  String get questionsAnswered => 'Questions answered';

  @override
  String get lessonsCompleted => 'Lessons completed';

  @override
  String get todaysPlan => 'Today\'s plan';

  @override
  String get planBasis =>
      'Suggested from your syllabus progress, recent accuracy and daily study time.';

  @override
  String get viewFullPlan => 'View full plan';

  @override
  String get planEmpty =>
      'Nothing planned yet. Enroll in a course to get a daily plan.';

  @override
  String get continueLearning => 'Continue learning';

  @override
  String get myCourses => 'My courses';

  @override
  String get recentResults => 'Recent results';

  @override
  String get weakTopics => 'Topics to strengthen';

  @override
  String accuracyOn(Object accuracy, Object count) {
    return '$accuracy% accuracy on $count questions';
  }

  @override
  String get practise => 'Practise';

  @override
  String get browseCourses => 'Browse courses';

  @override
  String get noCoursesYet => 'You haven\'t enrolled in a course yet.';

  @override
  String minutesShort(Object minutes) {
    return '$minutes min';
  }

  @override
  String percentComplete(Object percent) {
    return '$percent% complete';
  }

  @override
  String get searchHint => 'Search courses, topics, tests';

  @override
  String get search => 'Search';

  @override
  String get noResults => 'No results';

  @override
  String get allCourses => 'All';

  @override
  String get free => 'Free';

  @override
  String get paid => 'Paid';

  @override
  String get enrolled => 'Enrolled';

  @override
  String get coursesEmpty => 'No courses match.';

  @override
  String get enrollFree => 'Enroll for free';

  @override
  String buyFor(Object price) {
    return 'Buy for $price';
  }

  @override
  String accessDays(Object days) {
    return '$days days access';
  }

  @override
  String get lifetimeAccess => 'Lifetime access';

  @override
  String get youHaveAccess => 'You have access to this course';

  @override
  String get enrolledToast => 'You\'re enrolled';

  @override
  String get syllabus => 'Syllabus';

  @override
  String topicsCount(num count) {
    String _temp0 = intl.Intl.pluralLogic(
      count,
      locale: localeName,
      other: '$count topics',
      one: '1 topic',
    );
    return '$_temp0';
  }

  @override
  String get objectives => 'What you\'ll learn';

  @override
  String get video => 'Video';

  @override
  String get notes => 'Notes';

  @override
  String get freePreview => 'Free preview';

  @override
  String get locked => 'Locked';

  @override
  String get completed => 'Completed';

  @override
  String practiseTopic(Object count) {
    return 'Practise this topic ($count questions)';
  }

  @override
  String get noPracticeQuestions => 'No practice questions for this topic yet.';

  @override
  String get previousTopic => 'Previous topic';

  @override
  String get nextTopic => 'Next topic';

  @override
  String get unlockToContinue => 'Buy the course to unlock this lesson.';

  @override
  String get viewCourse => 'View course';

  @override
  String get videoUnavailable => 'This video can\'t be played right now.';

  @override
  String get playbackSpeed => 'Playback speed';

  @override
  String resumedAt(Object time) {
    return 'Resumed from $time';
  }

  @override
  String get openNotes => 'Open notes';

  @override
  String get markAsRead => 'Mark as read';

  @override
  String get markedDone => 'Marked as done';

  @override
  String get practiceTitle => 'Practice';

  @override
  String get practiceMistakes => 'Practise your mistakes';

  @override
  String get practiceMistakesHint =>
      'Questions you got wrong in practice and tests';

  @override
  String get practiceBookmarked => 'Bookmarked questions';

  @override
  String get practiceBookmarkedHint => 'Questions you saved for revision';

  @override
  String get practicePyq => 'Previous-year questions';

  @override
  String get practicePyqHint => 'From past papers, where licensed';

  @override
  String get practiceByTopic => 'Practise by topic';

  @override
  String get practiceByTopicHint => 'Open any topic in Learn and tap Practise';

  @override
  String questionOf(Object current, Object total) {
    return 'Question $current of $total';
  }

  @override
  String get checkAnswer => 'Check answer';

  @override
  String get correct => 'Correct';

  @override
  String get incorrect => 'Incorrect';

  @override
  String get partiallyCorrect => 'Partially correct';

  @override
  String get skipped => 'Skipped';

  @override
  String get explanation => 'Explanation';

  @override
  String correctAnswer(Object answer) {
    return 'Correct answer: $answer';
  }

  @override
  String yourAnswer(Object answer) {
    return 'Your answer: $answer';
  }

  @override
  String get notAnswered => 'Not answered';

  @override
  String get finish => 'Finish';

  @override
  String get practiceSummary => 'Practice summary';

  @override
  String get accuracy => 'Accuracy';

  @override
  String get done => 'Done';

  @override
  String get enterNumber => 'Enter your answer';

  @override
  String get selectAllThatApply => 'Select all that apply';

  @override
  String get hint => 'Hint';

  @override
  String get bookmark => 'Save for revision';

  @override
  String get bookmarked => 'Saved for revision';

  @override
  String get reportQuestion => 'Report a problem';

  @override
  String get reportReasonWrong => 'The answer key is wrong';

  @override
  String get reportReasonTypo => 'Typo or formatting';

  @override
  String get reportReasonUnclear => 'The question is unclear';

  @override
  String get reportReasonOther => 'Something else';

  @override
  String get reportDetails => 'Details (optional)';

  @override
  String get send => 'Send';

  @override
  String get cancel => 'Cancel';

  @override
  String get reportThanks => 'Thanks, our team will review it.';

  @override
  String get noQuestionsForSelection => 'No questions available here yet.';

  @override
  String get testsTitle => 'Tests';

  @override
  String get testsEmpty => 'No tests available yet.';

  @override
  String questionsCount(num count) {
    String _temp0 = intl.Intl.pluralLogic(
      count,
      locale: localeName,
      other: '$count questions',
      one: '1 question',
    );
    return '$_temp0';
  }

  @override
  String marksCount(Object marks) {
    return '$marks marks';
  }

  @override
  String minutesCount(Object minutes) {
    return '$minutes min';
  }

  @override
  String attemptsUsed(Object used, Object max) {
    return 'Attempts: $used of $max';
  }

  @override
  String get startTest => 'Start test';

  @override
  String get resumeTest => 'Resume test';

  @override
  String get retakeTest => 'Retake';

  @override
  String get viewResult => 'View result';

  @override
  String get instructions => 'Instructions';

  @override
  String get sections => 'Sections';

  @override
  String negativeMarking(Object marks) {
    return '−$marks for each wrong answer';
  }

  @override
  String get noNegativeMarking => 'No negative marking';

  @override
  String get testRules =>
      'The timer keeps running if you leave. Answers are saved as you go, and the test submits automatically when time runs out.';

  @override
  String get timeLeft => 'Time left';

  @override
  String get markForReview => 'Mark for review';

  @override
  String get unmarkReview => 'Unmark review';

  @override
  String get clearResponse => 'Clear';

  @override
  String get previous => 'Previous';

  @override
  String get saveAndNext => 'Save & next';

  @override
  String get submitTest => 'Submit test';

  @override
  String get questionPalette => 'Questions';

  @override
  String get paletteAnswered => 'Answered';

  @override
  String get paletteNotAnswered => 'Not answered';

  @override
  String get paletteMarked => 'Marked for review';

  @override
  String get paletteNotVisited => 'Not visited';

  @override
  String get submitConfirmTitle => 'Submit the test?';

  @override
  String submitConfirmBody(Object answered, Object marked, Object unanswered) {
    return 'Answered: $answered\nMarked for review: $marked\nNot answered: $unanswered\n\nYou can\'t change answers after submitting.';
  }

  @override
  String get submit => 'Submit';

  @override
  String get keepWorking => 'Keep working';

  @override
  String get leaveTestTitle => 'Leave the test?';

  @override
  String get leaveTestBody =>
      'Your answers are saved, but the timer keeps running. You can resume from the Tests tab.';

  @override
  String get leave => 'Leave';

  @override
  String get stay => 'Stay';

  @override
  String get saving => 'Saving…';

  @override
  String get saved => 'Saved';

  @override
  String get offlineWillRetry =>
      'Offline. Answers will be saved when you\'re back online.';

  @override
  String get timeUp => 'Time\'s up. Submitting your test…';

  @override
  String get resultTitle => 'Result';

  @override
  String get score => 'Score';

  @override
  String scoreOf(Object score, Object max) {
    return '$score / $max';
  }

  @override
  String get timeTaken => 'Time taken';

  @override
  String percentileLine(Object percent) {
    return 'Better than $percent% of first attempts on this platform';
  }

  @override
  String get notOfficialRank => 'Not an official rank.';

  @override
  String get subjectWise => 'Subject-wise';

  @override
  String get chapterWise => 'Chapter-wise';

  @override
  String get recommendations => 'What to do next';

  @override
  String get reviewAnswers => 'Review answers';

  @override
  String get backToTests => 'Back to tests';

  @override
  String get filterAll => 'All';

  @override
  String get filterIncorrect => 'Incorrect';

  @override
  String get filterSkipped => 'Skipped';

  @override
  String marksAwarded(Object marks) {
    return '$marks marks';
  }

  @override
  String get autoSubmitted => 'Submitted automatically when time ran out';

  @override
  String attemptNumber(Object number) {
    return 'Attempt $number';
  }

  @override
  String get planTitle => 'Study plan';

  @override
  String get regeneratePlan => 'Refresh plan';

  @override
  String get skip => 'Skip';

  @override
  String plannedMinutes(Object planned, Object daily) {
    return '$planned of $daily min planned';
  }

  @override
  String get lastSevenDays => 'Last 7 days';

  @override
  String tasksDone(Object done, Object total) {
    return '$done/$total done';
  }

  @override
  String get profileTitle => 'Profile';

  @override
  String preparingFor(Object exam, Object year) {
    return 'Preparing for $exam $year';
  }

  @override
  String get editPreferences => 'Study preferences';

  @override
  String get myProgress => 'My progress';

  @override
  String get myPurchases => 'My purchases';

  @override
  String get bookmarksTitle => 'Saved for revision';

  @override
  String get notificationsTitle => 'Notifications';

  @override
  String get helpSupport => 'Help & support';

  @override
  String get privacyPolicy => 'Privacy policy';

  @override
  String get deleteAccount => 'Delete my account';

  @override
  String get deleteAccountBody =>
      'We\'ll delete your account and personal data after any open orders or refunds are settled. You\'ll be signed out now.';

  @override
  String get delete => 'Delete';

  @override
  String get save => 'Save';

  @override
  String get savedToast => 'Saved';

  @override
  String get notificationsEmpty => 'No notifications yet.';

  @override
  String get markAllRead => 'Mark all as read';

  @override
  String get bookmarksEmpty =>
      'Nothing saved yet. Tap the bookmark on a question to save it.';

  @override
  String get remove => 'Remove';

  @override
  String get ordersEmpty => 'No purchases yet.';

  @override
  String get orderPaid => 'Paid';

  @override
  String get orderPending => 'Pending';

  @override
  String get orderFailed => 'Failed';

  @override
  String get orderRefunded => 'Refunded';

  @override
  String get ticketsEmpty => 'You haven\'t contacted support yet.';

  @override
  String get newTicket => 'Contact support';

  @override
  String get ticketCategory => 'Topic';

  @override
  String get categoryPayment => 'Payment';

  @override
  String get categoryContent => 'Course content';

  @override
  String get categoryTechnical => 'App problem';

  @override
  String get categoryAccount => 'Account';

  @override
  String get categoryOther => 'Other';

  @override
  String get ticketSubject => 'Subject';

  @override
  String get ticketMessage => 'How can we help?';

  @override
  String get ticketSent => 'Message sent. We\'ll reply here and notify you.';

  @override
  String get supportReply => 'Reply from support';

  @override
  String get progressTitle => 'My progress';

  @override
  String get testsTaken => 'Tests taken';

  @override
  String get averageScore => 'Average score';

  @override
  String get practiceAccuracy => 'Practice accuracy';

  @override
  String get scoreTrend => 'Score trend';

  @override
  String get noTestsYet => 'Take a test to see your progress here.';

  @override
  String get purchaseOnMobileOnly =>
      'Purchases are available in the Android and iOS app.';

  @override
  String get paymentSuccess => 'Payment successful. The course is unlocked.';

  @override
  String get paymentFailed =>
      'Payment didn\'t complete. No money was taken, or it will be refunded automatically.';

  @override
  String get paymentVerifying => 'Confirming your payment…';

  @override
  String get purchaseRequired => 'Buy this course to unlock it.';

  @override
  String get loading => 'Loading…';

  @override
  String get emptyGeneric => 'Nothing here yet.';

  @override
  String get play => 'Play';

  @override
  String get pause => 'Pause';

  @override
  String get fullScreen => 'Full screen';

  @override
  String get exitFullScreen => 'Exit full screen';

  @override
  String get sessionActiveTitle => 'Already signed in on another device';

  @override
  String sessionActiveBody(String device, String time) {
    return 'This account is signed in on $device (last active $time). Continue here? That device will be signed out.';
  }

  @override
  String get continueHere => 'Continue here';

  @override
  String get sessionReplaced =>
      'You were signed out because your account was signed in on another device.';

  @override
  String get timeJustNow => 'just now';

  @override
  String timeMinutesAgo(int count) {
    String _temp0 = intl.Intl.pluralLogic(
      count,
      locale: localeName,
      other: '$count minutes ago',
      one: '1 minute ago',
    );
    return '$_temp0';
  }

  @override
  String timeHoursAgo(int count) {
    String _temp0 = intl.Intl.pluralLogic(
      count,
      locale: localeName,
      other: '$count hours ago',
      one: '1 hour ago',
    );
    return '$_temp0';
  }

  @override
  String timeDaysAgo(int count) {
    String _temp0 = intl.Intl.pluralLogic(
      count,
      locale: localeName,
      other: '$count days ago',
      one: '1 day ago',
    );
    return '$_temp0';
  }

  @override
  String get doubtsTitle => 'My doubts';

  @override
  String get askDoubt => 'Ask a doubt';

  @override
  String get doubtsIntro =>
      'Stuck on something? Ask a teacher. You\'ll get a notification when they reply.';

  @override
  String get yourDoubt => 'Your doubt';

  @override
  String get doubtHint =>
      'Describe where you\'re stuck: the question, the step, or what isn\'t clear.';

  @override
  String doubtAbout(Object title) {
    return 'About: $title';
  }

  @override
  String get doubtGeneral => 'General doubt';

  @override
  String get doubtSent => 'Doubt sent. A teacher will reply soon.';

  @override
  String get doubtTooShort => 'Write at least 5 characters.';

  @override
  String get doubtsEmpty => 'You haven\'t asked any doubts yet.';

  @override
  String get doubtStatusOpen => 'Waiting for a teacher';

  @override
  String get doubtStatusAnswered => 'Answered';

  @override
  String get doubtStatusResolved => 'Resolved';

  @override
  String doubtReplies(int count) {
    String _temp0 = intl.Intl.pluralLogic(
      count,
      locale: localeName,
      other: '$count replies',
      one: '1 reply',
      zero: 'No replies yet',
    );
    return '$_temp0';
  }

  @override
  String get doubtYou => 'You';

  @override
  String get doubtTeacher => 'Teacher';

  @override
  String get doubtFollowUp => 'Ask a follow-up';

  @override
  String get doubtMarkResolved => 'My doubt is solved';

  @override
  String get doubtResolvedHint =>
      'Still unclear? Ask a follow-up and a teacher will look again.';

  @override
  String get topicQuiz => 'Topic quiz';

  @override
  String topicQuizInfo(int count, Object percent) {
    String _temp0 = intl.Intl.pluralLogic(
      count,
      locale: localeName,
      other: '$count questions',
      one: '1 question',
    );
    return '$_temp0 · pass mark $percent%';
  }

  @override
  String get topicQuizCovered =>
      'You\'ve covered this topic. Check your understanding with a short quiz.';

  @override
  String get topicQuizNotCovered =>
      'Finish the lessons first, or test yourself now.';

  @override
  String topicDonePrompt(int count) {
    String _temp0 = intl.Intl.pluralLogic(
      count,
      locale: localeName,
      other: '$count-question',
      one: '1-question',
    );
    return 'Topic covered! Take a quick $_temp0 quiz.';
  }

  @override
  String get startQuiz => 'Start quiz';

  @override
  String get retakeQuiz => 'Retake quiz';

  @override
  String quizBest(Object percent) {
    return 'Best score $percent%';
  }

  @override
  String get quizPassed => 'Passed';

  @override
  String get quizNotPassed => 'Not passed yet';

  @override
  String get submitQuiz => 'Submit quiz';

  @override
  String quizUnanswered(int count) {
    String _temp0 = intl.Intl.pluralLogic(
      count,
      locale: localeName,
      other: '$count questions are unanswered.',
      one: '1 question is unanswered.',
    );
    return '$_temp0 Submit anyway?';
  }

  @override
  String get quizResult => 'Quiz result';

  @override
  String get quizPassedBody => 'Well done! You passed this topic\'s quiz.';

  @override
  String quizFailedBody(Object percent) {
    return 'You need $percent% to pass. Revise the lessons and try again.';
  }

  @override
  String get backToTopic => 'Back to topic';

  @override
  String buyOnWebsite(Object price) {
    return 'Buy on website · $price';
  }

  @override
  String get buyOnWebsiteHint =>
      'Opens our website to pay securely. Come back to the app afterwards: the course unlocks by itself.';

  @override
  String get notForSaleInApp => 'This course can\'t be bought in the app.';
}
