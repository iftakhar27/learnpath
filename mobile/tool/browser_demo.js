// Drives the Flutter app's web build (semantics enabled) in a phone-sized Chromium against the live dev API.
const fs = require('fs');
const { chromium } = require('playwright');
// Usage: build the web app with --dart-define=API_BASE_URL=http://localhost:5080 --dart-define=ENABLE_SEMANTICS=true
// --no-web-resources-cdn, serve build/web on :8090, run the API in Development, then: node tool/browser_demo.js
const OUT = (process.env.OUT_DIR || __dirname + '/../build/demo') + '/';
const APP = process.env.APP_URL || 'http://localhost:8090';
const API = process.env.API_URL || 'http://localhost:5080/api/v1';

/** Registers a fresh student and grants the paid sample course, so every run starts at onboarding. */
async function freshStudent() {
  const post = async (path, body, token) => {
    const res = await fetch(API + path, { method: 'POST', headers: { 'content-type': 'application/json', ...(token ? { authorization: `Bearer ${token}` } : {}) }, body: JSON.stringify(body) });
    if (!res.ok) throw new Error(`${path} -> ${res.status}`);
    return res.status === 204 ? null : res.json();
  };
  const email = `ananya.demo${Date.now()}@example.com`;
  const student = await post('/auth/register', { email, password: 'Passw0rd!x', fullName: 'Ananya Gupta', acceptTerms: true });
  const admin = await post('/auth/login', { email: 'admin@example.com', password: 'ChangeMe!2026' });
  const courses = await (await fetch(API + '/courses?free=false')).json();
  const paid = courses.items.find((c) => c.slug === 'neet-physics-mechanics-sample');
  await post(`/admin/users/${student.userId}/enrollments`, { courseId: paid.id, reason: 'Demo account' }, admin.accessToken);
  return email;
}

fs.mkdirSync(OUT, { recursive: true });
let page;
const errors = [];
(async () => {
  const browser = await chromium.launch();
  const ctx = await browser.newContext({ viewport: { width: 390, height: 844 }, deviceScaleFactor: 2, isMobile: true, hasTouch: true });
  page = await ctx.newPage();
  const demoEmail = process.env.DEMO_EMAIL || (await freshStudent());
  page.on('pageerror', (e) => errors.push(e.message));
  let n = 0;
  const shot = async (name) => {
    await page.mouse.move(380, 400); // park the pointer off controls so hover tooltips don't appear
    await page.waitForTimeout(1600);
    const file = `${OUT}${String(++n).padStart(2, '0')}-${name}.png`;
    await page.screenshot({ path: file });
    console.log('shot', file.split('/').pop());
  };
  const button = (name) => page.getByRole('button', { name, exact: false }).first();
  const text = (t) => page.getByText(t, { exact: false }).first();
  // Section titles are exposed as group/heading labels rather than text, so wait on any of them.
  const see = (name, timeout = 20000) =>
    ['heading', 'group', 'button', 'tab', 'radio', 'checkbox', 'progressbar']
      .map((role) => page.getByRole(role, { name }))
      .reduce((a, b) => a.or(b))
      .or(page.getByText(name))
      .first()
      .waitFor({ timeout });
  // Flutter exposes controls by role (radio list tiles, checkable chips, tabs), so try roles before plain text.
  const any = (name) =>
    ['button', 'radio', 'checkbox', 'tab', 'link', 'menuitem']
      .map((role) => page.getByRole(role, { name }))
      .reduce((a, b) => a.or(b))
      .or(page.getByText(name))
      .first();
  // Flutter hit-tests by coordinates; when an overlay semantics node covers the target, click its position.
  const tapAny = async (name) => {
    const l = any(name);
    await l.waitFor({ timeout: 20000 });
    await l.click({ timeout: 3000 }).catch(() => l.click({ force: true }));
  };
  const tapText = tapAny;
  const tapButton = tapAny;
  const fill = async (label, value) => {
    const field = page.getByRole('textbox', { name: label }).first();
    await field.waitFor({ timeout: 20000 });
    await field.click();
    await field.fill(value);
  };

  // Answers whatever the current question is: types a number for numerical ones, otherwise picks an option.
  const answer = async (optionLetter = 'A', number = '20') => {
    const numeric = page.getByRole('textbox', { name: 'Enter your answer' });
    if (await numeric.count()) {
      await numeric.first().click({ force: true });
      await page.waitForTimeout(300);
      await page.keyboard.type(number); // Flutter's editing host takes key events, not fill()
    } else {
      const option = page.getByRole('button', { name: new RegExp(`^${optionLetter}\\.`) }).first();
      await option.click({ timeout: 3000 }).catch(() => option.click({ force: true }));
    }
    await page.waitForTimeout(400);
  };

  await page.goto(APP);
  await see('Continue with mobile number', 60000);
  await shot('welcome');

  await tapText('हिंदी');
  await see('मोबाइल नंबर से जारी रखें');
  await shot('welcome-hindi');
  await tapText('English');

  await tapButton('Sign in with email');
  await fill('Email', demoEmail);
  await fill('Password', process.env.DEMO_PASSWORD || 'Passw0rd!x');
  await shot('sign-in');
  await page.getByRole('button', { name: 'Sign in', exact: true }).click({ force: true });

  // Onboarding
  await see('Which exam are you preparing for?', 30000);
  await tapText('NEET (UG)');
  await tapText('2027');
  await shot('onboarding-exam');
  await tapButton('Next');
  await tapText('Class 11');
  await tapText('CBSE');
  await shot('onboarding-class');
  await tapButton('Next');
  await shot('onboarding-study-time');
  await tapButton('Get started');

  // Home
  await see("Today's plan", 30000);
  await shot('home');

  // Learn → course → topic
  await tapAny('Learn');
  await see('NEET Physics: Mechanics (Sample)', 20000);
  await shot('catalog');
  await tapText('NEET Physics: Mechanics (Sample)');
  await see('Syllabus');
  await shot('course');
  await tapText('Friction');
  await see('What you', 20000);
  await shot('topic');

  // Practice from the topic
  await tapButton('Practise this topic');
  await see('Question 1 of', 20000);
  await shot('practice-question');
  await answer('A');
  await tapButton('Check answer');
  await see('Explanation', 20000);
  await shot('practice-feedback');
  await page.goBack();
  await page.goBack();
  await page.goBack();

  // Tests
  await tapAny('Tests');
  await see('Laws of Motion', 20000);
  await shot('tests');
  const card = page.locator('flt-semantics').filter({ hasText: 'Laws of Motion' }).first();
  await card.getByRole('button', { name: /Start test|Resume test|Retake/ }).first().click().catch(async () => tapButton('Start test'));
  await see('Instructions', 20000);
  await shot('test-instructions');
  await page.getByRole('button', { name: /Start test|Resume test/ }).last().click();
  await see('Question 1 of', 30000);
  await answer('A');
  await shot('exam');
  await tapButton('Save & next');
  await answer('B', '18');
  await tapButton('Mark for review');
  await tapButton('Questions');
  await see('Not visited');
  await shot('exam-palette');
  await page.keyboard.press('Escape');
  await page.mouse.click(195, 150);
  await page.waitForTimeout(500);
  await page.getByRole('button', { name: 'Submit', exact: true }).first().click();
  await see('Submit the test?');
  await shot('exam-submit-confirm');
  await page.getByRole('button', { name: 'Submit', exact: true }).last().click();
  await see('Review answers', 30000);
  await shot('result');
  await tapButton('Review answers');
  await see('Explanation', 20000).catch(() => see('All', 5000));
  await shot('review');
  await page.goBack();
  await tapAny('Back to tests');

  // Profile and Hindi home
  await tapAny('Profile');
  await see('Study preferences', 20000);
  await shot('profile');
  await tapText('हिंदी');
  await tapAny('होम');
  await see('आज की योजना', 20000);
  await shot('home-hindi');

  console.log('page errors:', JSON.stringify(errors));
  await browser.close();
})().catch(async (e) => {
  console.error(String(e).split('\n')[0]);
  console.error('page errors:', JSON.stringify(errors).slice(0, 1500));
  if (page) {
    await page.screenshot({ path: OUT + 'zz-failure.png' }).catch(() => {});
    console.error((await page.locator('body').ariaSnapshot().catch(() => '')).slice(0, 2500));
  }
  process.exit(1);
});
