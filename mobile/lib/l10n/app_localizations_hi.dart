// ignore: unused_import
import 'package:intl/intl.dart' as intl;

import 'app_localizations.dart';

// ignore_for_file: type=lint

/// The translations for Hindi (`hi`).
class AppLocalizationsHi extends AppLocalizations {
  AppLocalizationsHi([String locale = 'hi']) : super(locale);

  @override
  String get appTagline => 'NEET, JEE और बोर्ड परीक्षाओं की तैयारी करें';

  @override
  String get continueWithOtp => 'मोबाइल नंबर से जारी रखें';

  @override
  String get continueWithEmail => 'ईमेल से साइन इन करें';

  @override
  String get createAccount => 'खाता बनाएँ';

  @override
  String get language => 'भाषा';

  @override
  String get english => 'English';

  @override
  String get hindi => 'हिंदी';

  @override
  String get email => 'ईमेल';

  @override
  String get password => 'पासवर्ड';

  @override
  String get fullName => 'पूरा नाम';

  @override
  String get signIn => 'साइन इन करें';

  @override
  String get signOut => 'साइन आउट करें';

  @override
  String get newHereCreateAccount => 'नए हैं? खाता बनाएँ';

  @override
  String get haveAccountSignIn => 'पहले से खाता है? साइन इन करें';

  @override
  String get acceptTerms => 'मैं सेवा की शर्तों और गोपनीयता नीति से सहमत हूँ';

  @override
  String get viewTerms => 'शर्तें और गोपनीयता नीति पढ़ें';

  @override
  String get mobileNumber => 'मोबाइल नंबर';

  @override
  String get sendOtp => 'OTP भेजें';

  @override
  String get enterOtp => '6 अंकों का OTP दर्ज करें';

  @override
  String otpSentTo(Object phone) {
    return '$phone पर OTP भेजा गया';
  }

  @override
  String get verify => 'सत्यापित करें';

  @override
  String resendIn(Object seconds) {
    return '$seconds सेकंड में दोबारा भेजें';
  }

  @override
  String get resendOtp => 'OTP दोबारा भेजें';

  @override
  String get yourNameNewAccounts => 'आपका नाम (नए खाते के लिए)';

  @override
  String get changeNumber => 'नंबर बदलें';

  @override
  String get fieldRequired => 'यह जानकारी ज़रूरी है';

  @override
  String get invalidEmail => 'सही ईमेल दर्ज करें';

  @override
  String get passwordTooShort => 'कम से कम 8 अक्षर रखें';

  @override
  String get invalidPhone => '10 अंकों का मोबाइल नंबर दर्ज करें';

  @override
  String get mustAcceptTerms => 'आगे बढ़ने के लिए शर्तें स्वीकार करें';

  @override
  String get errorNetwork =>
      'इंटरनेट कनेक्शन नहीं है। नेटवर्क जाँचें और फिर कोशिश करें।';

  @override
  String get errorGeneric => 'कुछ गड़बड़ हो गई। कृपया फिर से कोशिश करें।';

  @override
  String get retry => 'फिर से कोशिश करें';

  @override
  String get serverUnreachable => 'सर्वर से संपर्क नहीं हो पा रहा';

  @override
  String get sessionExpired =>
      'आपका सत्र समाप्त हो गया। कृपया फिर से साइन इन करें।';

  @override
  String get onboardingTitle => 'अपनी तैयारी सेट करें';

  @override
  String get chooseExam => 'आप किस परीक्षा की तैयारी कर रहे हैं?';

  @override
  String get targetYear => 'लक्ष्य वर्ष';

  @override
  String get yourClass => 'आपकी कक्षा';

  @override
  String get classDropper => 'ड्रॉपर';

  @override
  String classLabel(Object level) {
    return 'कक्षा $level';
  }

  @override
  String get board => 'बोर्ड';

  @override
  String get stateBoard => 'राज्य बोर्ड';

  @override
  String get dailyStudyTime => 'रोज़ पढ़ाई का समय';

  @override
  String minutesPerDay(Object minutes) {
    return 'रोज़ $minutes मिनट';
  }

  @override
  String get under18 => 'मेरी उम्र 18 वर्ष से कम है';

  @override
  String get guardianName => 'माता-पिता या अभिभावक का नाम';

  @override
  String get guardianConsent =>
      'मेरे माता-पिता/अभिभावक इस ऐप के उपयोग के लिए सहमत हैं';

  @override
  String get next => 'आगे';

  @override
  String get back => 'पीछे';

  @override
  String get getStarted => 'शुरू करें';

  @override
  String get selectExamError => 'आगे बढ़ने के लिए परीक्षा चुनें';

  @override
  String get tabHome => 'होम';

  @override
  String get tabLearn => 'सीखें';

  @override
  String get tabPractice => 'अभ्यास';

  @override
  String get tabTests => 'टेस्ट';

  @override
  String get tabProfile => 'प्रोफ़ाइल';

  @override
  String greeting(Object name) {
    return 'नमस्ते, $name';
  }

  @override
  String daysToExam(num days, Object exam) {
    String _temp0 = intl.Intl.pluralLogic(
      days,
      locale: localeName,
      other: '$exam में $days दिन',
      one: '$exam में 1 दिन',
      zero: '$exam आज है',
    );
    return '$_temp0';
  }

  @override
  String streakDays(num days) {
    String _temp0 = intl.Intl.pluralLogic(
      days,
      locale: localeName,
      other: '$days दिन लगातार',
      one: '1 दिन लगातार',
      zero: 'आज से लगातार पढ़ाई शुरू करें',
    );
    return '$_temp0';
  }

  @override
  String get thisWeek => 'इस सप्ताह';

  @override
  String get questionsAnswered => 'उत्तर दिए गए प्रश्न';

  @override
  String get lessonsCompleted => 'पूरे किए गए पाठ';

  @override
  String get todaysPlan => 'आज की योजना';

  @override
  String get planBasis =>
      'आपकी सिलेबस प्रगति, हाल की सटीकता और रोज़ के पढ़ाई समय के आधार पर सुझाव।';

  @override
  String get viewFullPlan => 'पूरी योजना देखें';

  @override
  String get planEmpty =>
      'अभी कोई योजना नहीं। रोज़ की योजना पाने के लिए किसी कोर्स में नामांकन करें।';

  @override
  String get continueLearning => 'पढ़ाई जारी रखें';

  @override
  String get myCourses => 'मेरे कोर्स';

  @override
  String get recentResults => 'हाल के परिणाम';

  @override
  String get weakTopics => 'जिन विषयों पर ध्यान दें';

  @override
  String accuracyOn(Object accuracy, Object count) {
    return '$count प्रश्नों पर $accuracy% सटीकता';
  }

  @override
  String get practise => 'अभ्यास करें';

  @override
  String get browseCourses => 'कोर्स देखें';

  @override
  String get noCoursesYet => 'आपने अभी किसी कोर्स में नामांकन नहीं किया है।';

  @override
  String minutesShort(Object minutes) {
    return '$minutes मिनट';
  }

  @override
  String percentComplete(Object percent) {
    return '$percent% पूरा';
  }

  @override
  String get searchHint => 'कोर्स, विषय, टेस्ट खोजें';

  @override
  String get search => 'खोजें';

  @override
  String get noResults => 'कोई परिणाम नहीं';

  @override
  String get allCourses => 'सभी';

  @override
  String get free => 'निःशुल्क';

  @override
  String get paid => 'सशुल्क';

  @override
  String get enrolled => 'नामांकित';

  @override
  String get coursesEmpty => 'कोई कोर्स नहीं मिला।';

  @override
  String get enrollFree => 'निःशुल्क नामांकन करें';

  @override
  String buyFor(Object price) {
    return '$price में खरीदें';
  }

  @override
  String accessDays(Object days) {
    return '$days दिन की पहुँच';
  }

  @override
  String get lifetimeAccess => 'आजीवन पहुँच';

  @override
  String get youHaveAccess => 'आपके पास इस कोर्स की पहुँच है';

  @override
  String get enrolledToast => 'आपका नामांकन हो गया';

  @override
  String get syllabus => 'पाठ्यक्रम';

  @override
  String topicsCount(num count) {
    String _temp0 = intl.Intl.pluralLogic(
      count,
      locale: localeName,
      other: '$count विषय',
      one: '1 विषय',
    );
    return '$_temp0';
  }

  @override
  String get objectives => 'आप क्या सीखेंगे';

  @override
  String get video => 'वीडियो';

  @override
  String get notes => 'नोट्स';

  @override
  String get freePreview => 'निःशुल्क झलक';

  @override
  String get locked => 'लॉक';

  @override
  String get completed => 'पूरा हुआ';

  @override
  String practiseTopic(Object count) {
    return 'इस विषय का अभ्यास करें ($count प्रश्न)';
  }

  @override
  String get noPracticeQuestions =>
      'इस विषय के लिए अभी अभ्यास प्रश्न नहीं हैं।';

  @override
  String get previousTopic => 'पिछला विषय';

  @override
  String get nextTopic => 'अगला विषय';

  @override
  String get unlockToContinue => 'यह पाठ खोलने के लिए कोर्स खरीदें।';

  @override
  String get viewCourse => 'कोर्स देखें';

  @override
  String get videoUnavailable => 'यह वीडियो अभी नहीं चल सकता।';

  @override
  String get playbackSpeed => 'चलाने की गति';

  @override
  String resumedAt(Object time) {
    return '$time से फिर शुरू किया';
  }

  @override
  String get openNotes => 'नोट्स खोलें';

  @override
  String get markAsRead => 'पढ़ा हुआ चिह्नित करें';

  @override
  String get markedDone => 'पूरा चिह्नित किया';

  @override
  String get practiceTitle => 'अभ्यास';

  @override
  String get practiceMistakes => 'अपनी गलतियों का अभ्यास करें';

  @override
  String get practiceMistakesHint => 'अभ्यास और टेस्ट में गलत हुए प्रश्न';

  @override
  String get practiceBookmarked => 'सहेजे गए प्रश्न';

  @override
  String get practiceBookmarkedHint => 'दोहराने के लिए सहेजे गए प्रश्न';

  @override
  String get practicePyq => 'पिछले वर्षों के प्रश्न';

  @override
  String get practicePyqHint => 'पिछले प्रश्नपत्रों से (जहाँ अनुमति है)';

  @override
  String get practiceByTopic => 'विषय के अनुसार अभ्यास';

  @override
  String get practiceByTopicHint =>
      'सीखें में कोई भी विषय खोलें और \'अभ्यास करें\' दबाएँ';

  @override
  String questionOf(Object current, Object total) {
    return 'प्रश्न $current / $total';
  }

  @override
  String get checkAnswer => 'उत्तर जाँचें';

  @override
  String get correct => 'सही';

  @override
  String get incorrect => 'गलत';

  @override
  String get partiallyCorrect => 'आंशिक रूप से सही';

  @override
  String get skipped => 'छोड़ा गया';

  @override
  String get explanation => 'व्याख्या';

  @override
  String correctAnswer(Object answer) {
    return 'सही उत्तर: $answer';
  }

  @override
  String yourAnswer(Object answer) {
    return 'आपका उत्तर: $answer';
  }

  @override
  String get notAnswered => 'उत्तर नहीं दिया';

  @override
  String get finish => 'समाप्त करें';

  @override
  String get practiceSummary => 'अभ्यास सारांश';

  @override
  String get accuracy => 'सटीकता';

  @override
  String get done => 'हो गया';

  @override
  String get enterNumber => 'अपना उत्तर दर्ज करें';

  @override
  String get selectAllThatApply => 'सभी सही विकल्प चुनें';

  @override
  String get hint => 'संकेत';

  @override
  String get bookmark => 'दोहराने के लिए सहेजें';

  @override
  String get bookmarked => 'दोहराने के लिए सहेजा गया';

  @override
  String get reportQuestion => 'समस्या बताएँ';

  @override
  String get reportReasonWrong => 'उत्तर कुंजी गलत है';

  @override
  String get reportReasonTypo => 'टाइपिंग या फ़ॉर्मेटिंग की गलती';

  @override
  String get reportReasonUnclear => 'प्रश्न स्पष्ट नहीं है';

  @override
  String get reportReasonOther => 'कुछ और';

  @override
  String get reportDetails => 'विवरण (वैकल्पिक)';

  @override
  String get send => 'भेजें';

  @override
  String get cancel => 'रद्द करें';

  @override
  String get reportThanks => 'धन्यवाद, हमारी टीम इसकी जाँच करेगी।';

  @override
  String get noQuestionsForSelection => 'यहाँ अभी कोई प्रश्न उपलब्ध नहीं है।';

  @override
  String get testsTitle => 'टेस्ट';

  @override
  String get testsEmpty => 'अभी कोई टेस्ट उपलब्ध नहीं है।';

  @override
  String questionsCount(num count) {
    String _temp0 = intl.Intl.pluralLogic(
      count,
      locale: localeName,
      other: '$count प्रश्न',
      one: '1 प्रश्न',
    );
    return '$_temp0';
  }

  @override
  String marksCount(Object marks) {
    return '$marks अंक';
  }

  @override
  String minutesCount(Object minutes) {
    return '$minutes मिनट';
  }

  @override
  String attemptsUsed(Object used, Object max) {
    return 'प्रयास: $used / $max';
  }

  @override
  String get startTest => 'टेस्ट शुरू करें';

  @override
  String get resumeTest => 'टेस्ट फिर से शुरू करें';

  @override
  String get retakeTest => 'फिर से दें';

  @override
  String get viewResult => 'परिणाम देखें';

  @override
  String get instructions => 'निर्देश';

  @override
  String get sections => 'खंड';

  @override
  String negativeMarking(Object marks) {
    return 'हर गलत उत्तर पर −$marks';
  }

  @override
  String get noNegativeMarking => 'कोई नकारात्मक अंकन नहीं';

  @override
  String get testRules =>
      'टेस्ट छोड़ने पर भी समय चलता रहता है। उत्तर अपने आप सहेजे जाते हैं और समय खत्म होने पर टेस्ट अपने आप जमा हो जाता है।';

  @override
  String get timeLeft => 'बचा समय';

  @override
  String get markForReview => 'समीक्षा के लिए चिह्नित करें';

  @override
  String get unmarkReview => 'समीक्षा चिह्न हटाएँ';

  @override
  String get clearResponse => 'मिटाएँ';

  @override
  String get previous => 'पिछला';

  @override
  String get saveAndNext => 'सहेजें और आगे';

  @override
  String get submitTest => 'टेस्ट जमा करें';

  @override
  String get questionPalette => 'प्रश्न सूची';

  @override
  String get paletteAnswered => 'उत्तर दिया';

  @override
  String get paletteNotAnswered => 'उत्तर नहीं दिया';

  @override
  String get paletteMarked => 'समीक्षा हेतु चिह्नित';

  @override
  String get paletteNotVisited => 'नहीं देखा';

  @override
  String get submitConfirmTitle => 'टेस्ट जमा करें?';

  @override
  String submitConfirmBody(Object answered, Object marked, Object unanswered) {
    return 'उत्तर दिए: $answered\nसमीक्षा हेतु चिह्नित: $marked\nउत्तर नहीं दिए: $unanswered\n\nजमा करने के बाद उत्तर नहीं बदले जा सकते।';
  }

  @override
  String get submit => 'जमा करें';

  @override
  String get keepWorking => 'जारी रखें';

  @override
  String get leaveTestTitle => 'टेस्ट छोड़ें?';

  @override
  String get leaveTestBody =>
      'आपके उत्तर सहेजे गए हैं, पर समय चलता रहेगा। आप टेस्ट टैब से फिर शुरू कर सकते हैं।';

  @override
  String get leave => 'छोड़ें';

  @override
  String get stay => 'रुकें';

  @override
  String get saving => 'सहेजा जा रहा है…';

  @override
  String get saved => 'सहेजा गया';

  @override
  String get offlineWillRetry => 'ऑफ़लाइन। ऑनलाइन होने पर उत्तर सहेजे जाएँगे।';

  @override
  String get timeUp => 'समय समाप्त। आपका टेस्ट जमा हो रहा है…';

  @override
  String get resultTitle => 'परिणाम';

  @override
  String get score => 'अंक';

  @override
  String scoreOf(Object score, Object max) {
    return '$score / $max';
  }

  @override
  String get timeTaken => 'लिया गया समय';

  @override
  String percentileLine(Object percent) {
    return 'इस प्लेटफ़ॉर्म पर पहले प्रयासों में $percent% से बेहतर';
  }

  @override
  String get notOfficialRank => 'यह आधिकारिक रैंक नहीं है।';

  @override
  String get subjectWise => 'विषयवार';

  @override
  String get chapterWise => 'अध्यायवार';

  @override
  String get recommendations => 'आगे क्या करें';

  @override
  String get reviewAnswers => 'उत्तरों की समीक्षा करें';

  @override
  String get backToTests => 'टेस्ट पर वापस जाएँ';

  @override
  String get filterAll => 'सभी';

  @override
  String get filterIncorrect => 'गलत';

  @override
  String get filterSkipped => 'छोड़े गए';

  @override
  String marksAwarded(Object marks) {
    return '$marks अंक';
  }

  @override
  String get autoSubmitted => 'समय समाप्त होने पर अपने आप जमा हुआ';

  @override
  String attemptNumber(Object number) {
    return 'प्रयास $number';
  }

  @override
  String get planTitle => 'अध्ययन योजना';

  @override
  String get regeneratePlan => 'योजना ताज़ा करें';

  @override
  String get skip => 'छोड़ें';

  @override
  String plannedMinutes(Object planned, Object daily) {
    return '$daily में से $planned मिनट की योजना';
  }

  @override
  String get lastSevenDays => 'पिछले 7 दिन';

  @override
  String tasksDone(Object done, Object total) {
    return '$done/$total पूरे';
  }

  @override
  String get profileTitle => 'प्रोफ़ाइल';

  @override
  String preparingFor(Object exam, Object year) {
    return '$exam $year की तैयारी';
  }

  @override
  String get editPreferences => 'पढ़ाई की प्राथमिकताएँ';

  @override
  String get myProgress => 'मेरी प्रगति';

  @override
  String get myPurchases => 'मेरी खरीदारी';

  @override
  String get bookmarksTitle => 'दोहराने के लिए सहेजे गए';

  @override
  String get notificationsTitle => 'सूचनाएँ';

  @override
  String get helpSupport => 'सहायता';

  @override
  String get privacyPolicy => 'गोपनीयता नीति';

  @override
  String get deleteAccount => 'मेरा खाता हटाएँ';

  @override
  String get deleteAccountBody =>
      'खुले ऑर्डर या रिफ़ंड निपटने के बाद हम आपका खाता और व्यक्तिगत डेटा हटा देंगे। अभी आप साइन आउट हो जाएँगे।';

  @override
  String get delete => 'हटाएँ';

  @override
  String get save => 'सहेजें';

  @override
  String get savedToast => 'सहेजा गया';

  @override
  String get notificationsEmpty => 'अभी कोई सूचना नहीं।';

  @override
  String get markAllRead => 'सभी को पढ़ा हुआ चिह्नित करें';

  @override
  String get bookmarksEmpty =>
      'अभी कुछ सहेजा नहीं गया। किसी प्रश्न पर बुकमार्क दबाकर सहेजें।';

  @override
  String get remove => 'हटाएँ';

  @override
  String get ordersEmpty => 'अभी कोई खरीदारी नहीं।';

  @override
  String get orderPaid => 'भुगतान हुआ';

  @override
  String get orderPending => 'लंबित';

  @override
  String get orderFailed => 'विफल';

  @override
  String get orderRefunded => 'वापस किया गया';

  @override
  String get ticketsEmpty => 'आपने अभी सहायता से संपर्क नहीं किया है।';

  @override
  String get newTicket => 'सहायता से संपर्क करें';

  @override
  String get ticketCategory => 'विषय';

  @override
  String get categoryPayment => 'भुगतान';

  @override
  String get categoryContent => 'कोर्स सामग्री';

  @override
  String get categoryTechnical => 'ऐप की समस्या';

  @override
  String get categoryAccount => 'खाता';

  @override
  String get categoryOther => 'अन्य';

  @override
  String get ticketSubject => 'विषय पंक्ति';

  @override
  String get ticketMessage => 'हम कैसे मदद कर सकते हैं?';

  @override
  String get ticketSent =>
      'संदेश भेजा गया। हम यहीं जवाब देंगे और आपको सूचित करेंगे।';

  @override
  String get supportReply => 'सहायता टीम का जवाब';

  @override
  String get progressTitle => 'मेरी प्रगति';

  @override
  String get testsTaken => 'दिए गए टेस्ट';

  @override
  String get averageScore => 'औसत अंक';

  @override
  String get practiceAccuracy => 'अभ्यास सटीकता';

  @override
  String get scoreTrend => 'अंकों का रुझान';

  @override
  String get noTestsYet => 'अपनी प्रगति यहाँ देखने के लिए कोई टेस्ट दें।';

  @override
  String get purchaseOnMobileOnly => 'खरीदारी Android और iOS ऐप में उपलब्ध है।';

  @override
  String get paymentSuccess => 'भुगतान सफल। कोर्स खुल गया है।';

  @override
  String get paymentFailed =>
      'भुगतान पूरा नहीं हुआ। कोई पैसा नहीं कटा, या वह अपने आप वापस हो जाएगा।';

  @override
  String get paymentVerifying => 'आपका भुगतान पुष्टि हो रहा है…';

  @override
  String get purchaseRequired => 'इसे खोलने के लिए कोर्स खरीदें।';

  @override
  String get loading => 'लोड हो रहा है…';

  @override
  String get emptyGeneric => 'अभी यहाँ कुछ नहीं है।';

  @override
  String get play => 'चलाएँ';

  @override
  String get pause => 'रोकें';

  @override
  String get fullScreen => 'पूरी स्क्रीन';

  @override
  String get exitFullScreen => 'पूरी स्क्रीन से बाहर';

  @override
  String get sessionActiveTitle => 'दूसरे डिवाइस पर पहले से साइन इन है';

  @override
  String sessionActiveBody(String device, String time) {
    return 'यह खाता $device पर साइन इन है (आखिरी बार $time सक्रिय)। यहाँ जारी रखें? उस डिवाइस से साइन आउट हो जाएगा।';
  }

  @override
  String get continueHere => 'यहाँ जारी रखें';

  @override
  String get sessionReplaced =>
      'आपको साइन आउट कर दिया गया क्योंकि आपका खाता किसी दूसरे डिवाइस पर साइन इन किया गया।';

  @override
  String get timeJustNow => 'अभी';

  @override
  String timeMinutesAgo(int count) {
    String _temp0 = intl.Intl.pluralLogic(
      count,
      locale: localeName,
      other: '$count मिनट पहले',
      one: '1 मिनट पहले',
    );
    return '$_temp0';
  }

  @override
  String timeHoursAgo(int count) {
    String _temp0 = intl.Intl.pluralLogic(
      count,
      locale: localeName,
      other: '$count घंटे पहले',
      one: '1 घंटा पहले',
    );
    return '$_temp0';
  }

  @override
  String timeDaysAgo(int count) {
    String _temp0 = intl.Intl.pluralLogic(
      count,
      locale: localeName,
      other: '$count दिन पहले',
      one: '1 दिन पहले',
    );
    return '$_temp0';
  }

  @override
  String get doubtsTitle => 'मेरे डाउट';

  @override
  String get askDoubt => 'डाउट पूछें';

  @override
  String get doubtsIntro =>
      'कहीं अटके हैं? टीचर से पूछें। जवाब आने पर आपको सूचना मिलेगी।';

  @override
  String get yourDoubt => 'आपका डाउट';

  @override
  String get doubtHint =>
      'बताएं कि कहाँ अटके हैं: कौन-सा सवाल, कौन-सा स्टेप, या क्या साफ़ नहीं है।';

  @override
  String doubtAbout(Object title) {
    return 'विषय: $title';
  }

  @override
  String get doubtGeneral => 'सामान्य डाउट';

  @override
  String get doubtSent => 'डाउट भेज दिया गया। टीचर जल्द जवाब देंगे।';

  @override
  String get doubtTooShort => 'कम से कम 5 अक्षर लिखें।';

  @override
  String get doubtsEmpty => 'आपने अभी तक कोई डाउट नहीं पूछा है।';

  @override
  String get doubtStatusOpen => 'टीचर के जवाब का इंतज़ार';

  @override
  String get doubtStatusAnswered => 'जवाब मिला';

  @override
  String get doubtStatusResolved => 'हल हो गया';

  @override
  String doubtReplies(int count) {
    String _temp0 = intl.Intl.pluralLogic(
      count,
      locale: localeName,
      other: '$count जवाब',
      one: '1 जवाब',
      zero: 'अभी कोई जवाब नहीं',
    );
    return '$_temp0';
  }

  @override
  String get doubtYou => 'आप';

  @override
  String get doubtTeacher => 'टीचर';

  @override
  String get doubtFollowUp => 'आगे पूछें';

  @override
  String get doubtMarkResolved => 'मेरा डाउट हल हो गया';

  @override
  String get doubtResolvedHint =>
      'अब भी साफ़ नहीं? आगे पूछें, टीचर फिर देखेंगे।';

  @override
  String get topicQuiz => 'टॉपिक क्विज़';

  @override
  String topicQuizInfo(int count, Object percent) {
    String _temp0 = intl.Intl.pluralLogic(
      count,
      locale: localeName,
      other: '$count प्रश्न',
      one: '1 प्रश्न',
    );
    return '$_temp0 · पास अंक $percent%';
  }

  @override
  String get topicQuizCovered =>
      'आपने यह टॉपिक पूरा कर लिया है। एक छोटे क्विज़ से अपनी समझ जाँचें।';

  @override
  String get topicQuizNotCovered => 'पहले पाठ पूरे करें, या अभी खुद को जाँचें।';

  @override
  String topicDonePrompt(int count) {
    String _temp0 = intl.Intl.pluralLogic(
      count,
      locale: localeName,
      other: '$count प्रश्नों का',
      one: '1 प्रश्न का',
    );
    return 'टॉपिक पूरा! अब $_temp0 छोटा क्विज़ दें।';
  }

  @override
  String get startQuiz => 'क्विज़ शुरू करें';

  @override
  String get retakeQuiz => 'क्विज़ फिर से दें';

  @override
  String quizBest(Object percent) {
    return 'सर्वश्रेष्ठ स्कोर $percent%';
  }

  @override
  String get quizPassed => 'पास';

  @override
  String get quizNotPassed => 'अभी पास नहीं';

  @override
  String get submitQuiz => 'क्विज़ जमा करें';

  @override
  String quizUnanswered(int count) {
    String _temp0 = intl.Intl.pluralLogic(
      count,
      locale: localeName,
      other: '$count प्रश्नों के उत्तर नहीं दिए गए हैं।',
      one: '1 प्रश्न का उत्तर नहीं दिया गया है।',
    );
    return '$_temp0 फिर भी जमा करें?';
  }

  @override
  String get quizResult => 'क्विज़ परिणाम';

  @override
  String get quizPassedBody => 'शाबाश! आपने इस टॉपिक का क्विज़ पास कर लिया।';

  @override
  String quizFailedBody(Object percent) {
    return 'पास होने के लिए $percent% चाहिए। पाठ दोहराएँ और फिर कोशिश करें।';
  }

  @override
  String get backToTopic => 'विषय पर वापस जाएँ';

  @override
  String buyOnWebsite(Object price) {
    return 'वेबसाइट पर खरीदें · $price';
  }

  @override
  String get buyOnWebsiteHint =>
      'सुरक्षित भुगतान के लिए हमारी वेबसाइट खुलेगी। भुगतान के बाद ऐप पर लौटें, कोर्स अपने-आप खुल जाएगा।';

  @override
  String get notForSaleInApp => 'यह कोर्स ऐप में नहीं खरीदा जा सकता।';

  @override
  String get changePassword => 'पासवर्ड बदलें';

  @override
  String get currentPassword => 'मौजूदा पासवर्ड';

  @override
  String get newPassword => 'नया पासवर्ड';

  @override
  String get confirmNewPassword => 'नए पासवर्ड की पुष्टि करें';

  @override
  String get passwordsDontMatch => 'पासवर्ड मेल नहीं खाते';

  @override
  String get passwordChangedToast =>
      'पासवर्ड बदल गया। दूसरे डिवाइस से साइन आउट कर दिया गया है।';

  @override
  String get forgotPassword => 'पासवर्ड भूल गए?';

  @override
  String get forgotPasswordTitle => 'अपना पासवर्ड रीसेट करें';

  @override
  String get forgotPasswordBody =>
      'वह ईमेल डालें जिससे आपने अकाउंट बनाया था। हम नया पासवर्ड चुनने का लिंक भेजेंगे।';

  @override
  String get sendResetLink => 'रीसेट लिंक भेजें';

  @override
  String resetLinkSent(Object email) {
    return 'अगर $email का अकाउंट है, तो हमने नया पासवर्ड चुनने का लिंक भेज दिया है। इनबॉक्स और स्पैम फ़ोल्डर देखें; लिंक 30 मिनट तक काम करेगा।';
  }

  @override
  String get backToSignIn => 'साइन इन पर वापस जाएँ';

  @override
  String get subjectsTitle => 'विषय';

  @override
  String get subjectsSubtitle => 'NCERT, अध्याय दर अध्याय';

  @override
  String classLevel(Object level) {
    return 'कक्षा $level';
  }

  @override
  String chaptersReady(int ready, int total) {
    return '$total में से $ready अध्याय तैयार';
  }

  @override
  String continueChapter(Object title) {
    return 'जारी रखें: $title';
  }

  @override
  String chapterNumber(int number) {
    return 'अध्याय $number';
  }

  @override
  String get chapterComingSoon => 'पाठ जल्द आ रहे हैं';

  @override
  String videosCount(int count) {
    String _temp0 = intl.Intl.pluralLogic(
      count,
      locale: localeName,
      other: '$count वीडियो',
      one: '1 वीडियो',
    );
    return '$_temp0';
  }

  @override
  String notesCount(int count) {
    String _temp0 = intl.Intl.pluralLogic(
      count,
      locale: localeName,
      other: '$count नोट्स',
      one: '1 नोट',
    );
    return '$_temp0';
  }

  @override
  String get videos => 'वीडियो';

  @override
  String get noVideosYet => 'इस अध्याय के वीडियो अभी उपलब्ध नहीं हैं।';

  @override
  String get noNotesYet => 'इस अध्याय के नोट्स अभी उपलब्ध नहीं हैं।';

  @override
  String practiseChapter(int count) {
    return 'इस अध्याय का अभ्यास करें ($count प्रश्न)';
  }

  @override
  String fromCourse(Object course) {
    return '$course से';
  }

  @override
  String get chapterLocked =>
      'यह अध्याय एक पेड कोर्स में है। सभी पाठ खोलने के लिए कोर्स खरीदें।';

  @override
  String get physicsShort => 'भौतिकी';

  @override
  String get chemistryShort => 'रसायन';

  @override
  String get biologyShort => 'जीव विज्ञान';

  @override
  String get coursesHeading => 'कोर्स';

  @override
  String get intro1Title => 'हर NCERT अध्याय, बस एक टैप पर';

  @override
  String get intro1Body =>
      'कक्षा 11 और 12 की भौतिकी, रसायन और जीव विज्ञान, हर अध्याय के वीडियो, नोट्स और अभ्यास के साथ।';

  @override
  String get intro2Title => 'NEET की तरह अभ्यास करें';

  @override
  String get intro2Body =>
      'अध्याय क्विज़, पिछले वर्षों के प्रश्न और समय वाले मॉक टेस्ट, तुरंत परिणाम और हल के साथ।';

  @override
  String get intro3Title => 'अटक गए? डाउट पूछें';

  @override
  String get intro3Body =>
      'अपना प्रश्न लिखें और टीचर जवाब देंगे। जवाब आने पर आपको सूचना मिलेगी।';

  @override
  String get intro4Title => 'योजना बनाएं, प्रगति देखें, सुधार करें';

  @override
  String get intro4Body =>
      'रोज़ की पढ़ाई की योजना, हर विषय में आपकी प्रगति और कमज़ोर टॉपिक, ताकि आप हमेशा जानें कि आगे क्या पढ़ना है।';

  @override
  String introProgress(int current, int total) {
    return 'स्लाइड $current / $total';
  }

  @override
  String get menuStudy => 'पढ़ाई';

  @override
  String get menuHelp => 'मदद';

  @override
  String get menuSettings => 'सेटिंग्स';

  @override
  String get menuSubjectsCourses => 'विषय और कोर्स';

  @override
  String get nightMode => 'नाइट मोड';

  @override
  String get appearance => 'थीम';

  @override
  String get themeAuto => 'ऑटो';

  @override
  String get themeLight => 'लाइट';

  @override
  String get themeDark => 'डार्क';

  @override
  String get customTestTitle => 'अपना टेस्ट बनाएं';

  @override
  String get customTestIntro =>
      'अध्याय, प्रश्नों की संख्या और कठिनाई चुनें। आपको असली परीक्षा जैसा समय वाला टेस्ट मिलेगा।';

  @override
  String get myCustomTests => 'मेरे बनाए टेस्ट';

  @override
  String get customTestBadge => 'आपका टेस्ट';

  @override
  String get chooseChapters => 'अध्याय चुनें';

  @override
  String get selectAll => 'सभी चुनें';

  @override
  String get clearSelection => 'हटाएं';

  @override
  String chaptersSelected(int count) {
    String _temp0 = intl.Intl.pluralLogic(
      count,
      locale: localeName,
      other: '$count अध्याय चुने',
      one: '1 अध्याय चुना',
      zero: 'कोई अध्याय नहीं चुना',
    );
    return '$_temp0';
  }

  @override
  String questionsAvailable(int count) {
    String _temp0 = intl.Intl.pluralLogic(
      count,
      locale: localeName,
      other: '$count प्रश्न उपलब्ध',
      one: '1 प्रश्न उपलब्ध',
      zero: 'कोई प्रश्न उपलब्ध नहीं',
    );
    return '$_temp0';
  }

  @override
  String moreInPaidCourses(int count) {
    String _temp0 = intl.Intl.pluralLogic(
      count,
      locale: localeName,
      other: '$count और पेड कोर्स में',
      one: '1 और पेड कोर्स में',
    );
    return '$_temp0';
  }

  @override
  String get onlyInPaidCourses => 'केवल पेड कोर्स में';

  @override
  String get numberOfQuestions => 'प्रश्नों की संख्या';

  @override
  String allQuestionsCount(int count) {
    return 'सभी ($count)';
  }

  @override
  String get difficulty => 'कठिनाई';

  @override
  String get difficultyAny => 'कोई भी';

  @override
  String get difficultyEasy => 'आसान';

  @override
  String get difficultyMedium => 'मध्यम';

  @override
  String get difficultyHard => 'कठिन';

  @override
  String get skipCorrect => 'जो प्रश्न पहले सही कर चुका हूँ, उन्हें छोड़ें';

  @override
  String get skipCorrectHint =>
      'दूसरे प्रश्न कम पड़ने पर ही ये जोड़े जाते हैं।';

  @override
  String get createTest => 'टेस्ट बनाएं';

  @override
  String get customTestNoChapters =>
      'अभी किसी अध्याय में प्रश्न नहीं हैं। जल्द ही देखें।';

  @override
  String get customTestPickChapter => 'कम से कम एक अध्याय चुनें';

  @override
  String get flashcardsTitle => 'फ्लैश कार्ड';

  @override
  String get flashcardsIntro =>
      'अध्याय-दर-अध्याय झटपट दोहराई। जो कार्ड आपको आते हैं, वे कम बार लौटते हैं।';

  @override
  String flashcardsDue(int count) {
    String _temp0 = intl.Intl.pluralLogic(
      count,
      locale: localeName,
      other: '$count कार्ड बाकी',
      one: '1 कार्ड बाकी',
      zero: 'अभी कुछ बाकी नहीं',
    );
    return '$_temp0';
  }

  @override
  String flashcardsCount(int count) {
    String _temp0 = intl.Intl.pluralLogic(
      count,
      locale: localeName,
      other: '$count कार्ड',
      one: '1 कार्ड',
    );
    return '$_temp0';
  }

  @override
  String flashcardsMastered(int count) {
    return '$count पक्के';
  }

  @override
  String get flashcardsEmpty => 'अभी कोई फ्लैश कार्ड नहीं है। जल्द ही देखें।';

  @override
  String flashcardsForChapter(int count) {
    String _temp0 = intl.Intl.pluralLogic(
      count,
      locale: localeName,
      other: '$count फ्लैश कार्ड',
      one: '1 फ्लैश कार्ड',
    );
    return '$_temp0';
  }

  @override
  String get cardFront => 'प्रश्न';

  @override
  String get cardBack => 'उत्तर';

  @override
  String get tapToFlip => 'उत्तर देखने के लिए कार्ड पर टैप करें';

  @override
  String get showAnswer => 'उत्तर दिखाएं';

  @override
  String get gotIt => 'आ गया';

  @override
  String get stillLearning => 'अभी सीख रहा हूँ';

  @override
  String cardProgress(int current, int total) {
    return 'कार्ड $current / $total';
  }

  @override
  String get sessionDone => 'सत्र पूरा हुआ';

  @override
  String sessionKnew(int known, int total) {
    return 'आपको $total में से $known आए।';
  }

  @override
  String sessionAgainSoon(int count) {
    String _temp0 = intl.Intl.pluralLogic(
      count,
      locale: localeName,
      other: '$count कार्ड जल्द फिर आएंगे।',
      one: '1 कार्ड जल्द फिर आएगा।',
      zero: '',
    );
    return '$_temp0';
  }

  @override
  String get allCaughtUp => 'सब हो गया!';

  @override
  String nextCardsDue(String date) {
    return 'अगले कार्ड $date को आएंगे।';
  }

  @override
  String get reviseAll => 'सभी कार्ड दोहराएं';

  @override
  String get studyNow => 'अभी पढ़ें';

  @override
  String get studyAgain => 'फिर से पढ़ें';

  @override
  String get allDecks => 'सभी डेक';

  @override
  String get exploreAsGuest => 'बिना खाते के देखें';

  @override
  String get guestTitle => 'आप मेहमान के रूप में देख रहे हैं';

  @override
  String get guestBody =>
      'विषय, अध्याय और कोर्स देखें। लेसन देखने, अभ्यास करने, टेस्ट देने और अपनी प्रगति देखने के लिए मुफ़्त खाता बनाएं।';

  @override
  String get guestDrawerBody =>
      'आराम से देखें। मुफ़्त खाते में आपकी प्रगति सहेजी जाती है।';

  @override
  String get createFreeAccount => 'मुफ़्त खाता बनाएं';

  @override
  String get coursesToExplore => 'देखने लायक कोर्स';

  @override
  String get guestLockedTitle => 'आगे बढ़ने के लिए मुफ़्त खाता बनाएं';

  @override
  String get guestLockedBody =>
      'लेसन देखने, अभ्यास करने और टेस्ट देने के लिए मुफ़्त खाता बनाएं। बस एक मिनट लगता है।';

  @override
  String get guestPracticeBody =>
      'तुरंत हल के साथ प्रश्नों का अभ्यास करें, फ्लैश कार्ड से दोहराएं और अपनी गलतियां फिर से देखें।';

  @override
  String get guestTestsBody =>
      'समय वाले मॉक टेस्ट दें, किसी भी अध्याय से अपना टेस्ट बनाएं और देखें कि कहां सुधार करना है।';

  @override
  String get guestProfileBody =>
      'अपनी प्रगति सहेजें, रोज़ की पढ़ाई की योजना पाएं और किसी भी डिवाइस पर वहीं से जारी रखें।';

  @override
  String get signUpToStart => 'शुरू करने के लिए मुफ़्त साइन अप करें';

  @override
  String get whatsAppChat => 'WhatsApp पर हमसे बात करें';

  @override
  String get whatsAppChannel => 'हमारे WhatsApp चैनल से जुड़ें';

  @override
  String get inviteFriends => 'WhatsApp पर दोस्तों को बुलाएं';

  @override
  String get shareOnWhatsApp => 'WhatsApp पर शेयर करें';

  @override
  String whatsAppHello(String brand) {
    return 'नमस्ते! मेरा $brand के बारे में एक सवाल है।';
  }

  @override
  String shareAppMessage(String brand, String url) {
    return 'मैं $brand के साथ NEET, JEE और बोर्ड की तैयारी कर रहा/रही हूँ: वीडियो, अभ्यास, मॉक टेस्ट और फ्लैश कार्ड। देखें: $url';
  }

  @override
  String shareCourseMessage(String course, String brand, String url) {
    return '$brand पर “$course” देखें: $url';
  }
}
