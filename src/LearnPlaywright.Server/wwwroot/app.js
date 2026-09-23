// COMP-01 フロントエンドUI（FUNC-01〜09）。フレームワーク不使用のプレーンJavaScript（CON-02）。
// DOM要素・fetch関数は引数で受け取る（依存性注入）ことで、各関数を単独で検証しやすくしている。
'use strict';

const MESSAGE_TYPES = ['success', 'error', 'info'];
const MESSAGE_CLASS_PREFIX = 'message--';

// FUNC-01: 4種のUIコントロールの現在値をFormValuesとして取得する
function collectFormValues(elements) {
  if (!elements || !elements.textInput || !elements.slider || !elements.select || !elements.radioButtons) {
    throw new TypeError('elements is incomplete');
  }
  const slider = Number(elements.slider.value);
  if (Number.isNaN(slider)) {
    throw new Error('slider value is not a number');
  }
  const checked = Array.from(elements.radioButtons).find((radio) => radio.checked);
  return {
    text: elements.textInput.value,
    slider: slider,
    select: elements.select.value,
    radio: checked ? checked.value : null,
  };
}

// FUNC-02: FormValuesをUIコントロールへ反映し復元する
function applyFormValues(elements, values) {
  if (!elements || !values) {
    throw new TypeError('elements and values are required');
  }
  elements.textInput.value = values.text;
  elements.slider.value = values.slider;

  // 選択肢に存在しない値は反映しない（選択状態を変更しない）
  const hasOption = Array.from(elements.select.options).some((option) => option.value === values.select);
  if (hasOption) {
    elements.select.value = values.select;
  }

  // radioがnull、または一致する選択肢がない場合は既存の選択状態を保持する
  if (values.radio !== null && values.radio !== undefined) {
    const target = Array.from(elements.radioButtons).find((radio) => radio.value === values.radio);
    if (target) {
      target.checked = true;
    }
  }
}

// FUNC-03: FormValuesをJSONでサーバーへPOST送信する
async function postFormValues(url, values, fetchFn = fetch) {
  const response = await fetchFn(url, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(values),
    credentials: 'same-origin', // 同一オリジンのCookie（ユーザー識別用）を送受信する
  });
  return { ok: response.ok, status: response.status };
}

// FUNC-04: 保存済みFormValuesをサーバーからGET取得する（404は「未存在」として正常系扱い）
async function fetchFormValues(url, fetchFn = fetch) {
  const response = await fetchFn(url, { method: 'GET', credentials: 'same-origin' });
  if (response.status === 200) {
    const values = await response.json(); // パース失敗時はrejectされる
    return { found: true, values: values, status: 200 };
  }
  if (response.status === 404) {
    return { found: false, values: null, status: 404 };
  }
  throw new Error('Unexpected HTTP status: ' + response.status);
}

// FUNC-05: メッセージ表示領域にテキストと種別を反映する
function displayMessage(messageElement, text, type) {
  if (!messageElement) {
    throw new TypeError('messageElement is required');
  }
  if (!MESSAGE_TYPES.includes(type)) {
    throw new Error('Unknown message type: ' + type);
  }
  MESSAGE_TYPES.forEach((t) => messageElement.classList.remove(MESSAGE_CLASS_PREFIX + t));
  messageElement.classList.add(MESSAGE_CLASS_PREFIX + type);
  messageElement.textContent = text;
}

// FUNC-06: メッセージ表示領域の内容をクリアする
function clearMessage(messageElement) {
  if (!messageElement) {
    throw new TypeError('messageElement is required');
  }
  MESSAGE_TYPES.forEach((t) => messageElement.classList.remove(MESSAGE_CLASS_PREFIX + t));
  messageElement.textContent = '';
}

// FUNC-07: 送信処理（収集→POST→結果表示）を統括する
async function handleSubmitButtonClick(event, deps) {
  event.preventDefault();
  try {
    const values = collectFormValues(deps.elements);
    const result = await postFormValues(deps.postUrl, values, deps.fetchFn || fetch);
    if (result.ok) {
      displayMessage(deps.messageElement, '保存しました。', 'success');
    } else {
      displayMessage(deps.messageElement, '保存できませんでした。入力内容を確認してください。（HTTP ' + result.status + '）', 'error');
    }
  } catch (e) {
    displayMessage(deps.messageElement, '保存中にエラーが発生しました。', 'error');
  }
}

// FUNC-08: 読み込み処理（GET→復元/未存在表示）を統括する
async function handleLoadButtonClick(event, deps) {
  event.preventDefault();
  try {
    const result = await fetchFormValues(deps.loadUrl, deps.fetchFn || fetch);
    if (result.found) {
      applyFormValues(deps.elements, result.values);
      clearMessage(deps.messageElement);
    } else {
      displayMessage(deps.messageElement, '保存されたデータがありません。', 'info');
    }
  } catch (e) {
    displayMessage(deps.messageElement, '読み込み中にエラーが発生しました。', 'error');
  }
}

// 処理完了ごとに data-request-count を1増やす（テストが「処理完了」を待つための目印。実装工程で追加）
function markRequestCompleted(formElement) {
  if (formElement) {
    const current = Number(formElement.dataset.requestCount || '0');
    formElement.dataset.requestCount = String(current + 1);
  }
}

// FUNC-09: 送信・読み込みボタンへイベントリスナーを登録する
function initializeApp(deps) {
  if (!deps || !deps.elements || !deps.submitButton || !deps.loadButton) {
    throw new TypeError('deps is incomplete');
  }
  deps.submitButton.addEventListener('click', async (event) => {
    await handleSubmitButtonClick(event, deps);
    markRequestCompleted(deps.formElement);
  });
  deps.loadButton.addEventListener('click', async (event) => {
    await handleLoadButtonClick(event, deps);
    markRequestCompleted(deps.formElement);
  });
}

// ブラウザ上では DOMContentLoaded 時に実際のDOM要素を結線して初期化する
if (typeof document !== 'undefined') {
  document.addEventListener('DOMContentLoaded', () => {
    initializeApp({
      elements: {
        textInput: document.getElementById('text-input'),
        slider: document.getElementById('slider'),
        select: document.getElementById('select'),
        radioButtons: Array.from(document.querySelectorAll('input[name="color"]')),
      },
      messageElement: document.getElementById('message-area'),
      submitButton: document.getElementById('submit-button'),
      loadButton: document.getElementById('load-button'),
      formElement: document.getElementById('user-form'),
      postUrl: '/api/form-data',
      loadUrl: '/api/form-data',
    });
  });
}

// Node.js等からの単体検証用エクスポート（ブラウザでは無視される）
if (typeof module !== 'undefined' && module.exports) {
  module.exports = {
    collectFormValues, applyFormValues, postFormValues, fetchFormValues,
    displayMessage, clearMessage, handleSubmitButtonClick, handleLoadButtonClick, initializeApp,
  };
}
