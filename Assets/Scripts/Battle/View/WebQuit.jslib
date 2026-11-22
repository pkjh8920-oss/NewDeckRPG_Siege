// 3-204: 웹에서 [종료] → 탭 닫기 시도, 막히면 종료 안내 화면 (WebQuit.cs 에서 부름)
mergeInto(LibraryManager.library, {
  WebQuitGame: function () {
    try { window.close(); } catch (e) {}
    // 0.3초 뒤에도 탭이 그대로면 = 브라우저가 닫기를 막은 것 → 게임 화면을 안내 문구로 바꿈
    setTimeout(function () {
      document.body.style.margin = '0';
      document.body.style.background = '#0C0A12';
      document.body.innerHTML =
        '<div style="height:100vh;display:flex;flex-direction:column;align-items:center;justify-content:center;' +
        'color:#E6DCC8;font-family:sans-serif;font-size:28px;gap:18px;">' +
        '<div>게임을 종료했습니다.</div>' +
        '<div style="font-size:18px;opacity:0.7;">이 탭을 닫거나, 새로고침(F5)하면 다시 시작합니다.</div></div>';
    }, 300);
  }
});
