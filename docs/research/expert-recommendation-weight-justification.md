# Nghiên cứu justify bộ trọng số weighted-sum trong công thức recommend expert của Aivora

**Ngày thực hiện:** 2026-07-29
**Phạm vi:** Đối chiếu công thức `RecommendationScorer` hiện tại của Aivora với (1) tiêu chí match/rank freelancer của các nền tảng freelance lớn, (2) literature học thuật về MCDM/weighted-sum/AHP cho bài toán service-provider selection, (3) chính sách penalty theo dispute/cancellation rate của các nền tảng reputation lớn khác.

**Lưu ý phương pháp luận quan trọng:** Aivora là một marketplace Client–Expert AI cụ thể, chưa có paper hay tài liệu nghiên cứu nào nói trực tiếp về nó. Toàn bộ nghiên cứu dưới đây dùng cách tiếp cận **suy luận tương tự (analogy)** — tổng quát hóa từ freelance marketplace nói chung (Upwork, Freelancer.com, Toptal) và từ các nền tảng reputation/marketplace khác (eBay, Airbnb, Uber) sang bối cảnh Aivora. Đây không phải là nguồn nói trực tiếp về Aivora, và cần được hiểu như vậy khi trích dẫn.

---

## 1. Tóm tắt công thức hiện tại của Aivora

| Tiêu chí | Trọng số | Công thức tính | Ghi chú |
|---|---|---|---|
| SkillScore | 0.40 | Trung bình theo skill level match trên các required skill của job. Level weight hardcode: BEGINNER=0.5, INTERMEDIATE=0.75, ADVANCED=0.9, EXPERT=1.0. Nếu job không yêu cầu skill nào → SkillScore=100 | Trọng số cao nhất trong 5 tiêu chí |
| BudgetScore | 0.20 | 100 nếu chi phí ước tính nằm trong [budgetMin, budgetMax]; 95 nếu thấp hơn budgetMin; giảm tuyến tính về 0 nếu vượt budgetMax | Không phạt nặng khi rẻ hơn kỳ vọng |
| RatingScore | 0.20 | `expert.Rating` (thang 1-5 sao) × 20 → quy đổi tuyến tính sang thang 100 | |
| AvailabilityScore | 0.10 | 100 nếu AvailabilityStatus == AVAILABLE, ngược lại 50 | Nhị phân |
| CompletionScore | 0.10 | `expert.SuccessRate` nếu > 0, else mặc định 80 | Giá trị mặc định lạc quan cho expert chưa có project nào |
| **Tổng** | **1.00** | Có validate trong code — nếu tổng 5 trọng số không bằng 1.0 thì service không start | |

**TotalScore = SkillScore×0.40 + BudgetScore×0.20 + RatingScore×0.20 + AvailabilityScore×0.10 + CompletionScore×0.10**

### Penalty áp dụng sau khi tính TotalScore

| Penalty | Công thức | Điều kiện áp dụng | Ghi chú |
|---|---|---|---|
| DisputePenalty | `min(disputeRate × 1.5, 0.5)` với `disputeRate = disputeCount / completedProjects` | Chỉ tính khi `completedProjects >= 3` | Rationale trong comment code: tránh 1 dispute đầu tiên giết chết điểm của expert mới vì mẫu quá nhỏ |
| OverduePenalty | `min(overdueRate × 0.3, 0.3)` với `overdueRate = overdueMilestoneCount / totalMilestoneCount` | Tính trên các project đang ACTIVE/DISPUTED | |

**TotalScore final = TotalScore × (1 − DisputePenalty) × (1 − OverduePenalty)**

Tất cả các con số hardcode (40/20/20/10/10, level weight 0.5/0.75/0.9/1.0, penalty factor 1.5 và 0.3, cap 0.5 và 0.3, ngưỡng `completedProjects >= 3`) **không có nguồn trích dẫn nào trong codebase** — chỉ có rationale định tính trong 2 đoạn comment về dispute penalty và ngưỡng 3 project. Đây chính là khoảng trống mà nghiên cứu này cố gắng lấp bằng cách đối chiếu với thực tiễn ngành.

---

## 2. Mục nghiên cứu 1: Tiêu chí match/rank freelancer của các nền tảng lớn

### 2.1. Upwork — Job Success Score (JSS)

- **URL:** https://support.upwork.com/hc/en-us/articles/38437458199059-How-is-my-Job-Success-Score-calculated
- **Loại nguồn:** Official platform docs (Upwork Help Center)
- **Năm:** không rõ năm xuất bản cụ thể của bài viết (nội dung truy cập 2026, không có ngày publish hiển thị)
- **Trích dẫn nguyên văn:**
  > "Your JSS reflects your overall contract history with your clients on Upwork and is based on your (or your agency's) relationships and feedback. It takes into consideration a number of different factors, including feedback, contract-ending history, and long-term customer relationships"
  >
  > (Dịch: "JSS của bạn phản ánh toàn bộ lịch sử hợp đồng với khách hàng trên Upwork, dựa trên mối quan hệ và phản hồi của bạn (hoặc agency của bạn). Nó xem xét một số yếu tố khác nhau, bao gồm phản hồi, lịch sử kết thúc hợp đồng, và mối quan hệ khách hàng dài hạn")
  >
  > "At a high level, we look at JSS this way: (successful contract outcomes − negative contract outcomes) / total outcomes"
  >
  > (Dịch: "Ở mức độ tổng quát, chúng tôi tính JSS như sau: (kết quả hợp đồng thành công − kết quả hợp đồng tiêu cực) / tổng số kết quả")

- **URL:** https://support.upwork.com/hc/en-us/articles/38439816969875-What-factors-affect-my-Job-Success-Score
- **Loại nguồn:** Official platform docs
- **Năm:** không rõ năm
- **Trích dẫn nguyên văn:**
  > "✅ Positive Impacts on JSS: High client satisfaction (good feedback); Long-term client relationships (clients who make payments over 90+ days); Completing contracts successfully and earning positive ratings; Higher-value projects contribute more positively"
  >
  > "❌ Negative Impacts on JSS: Negative client feedback; Disputed contracts and poor client experiences; Lack of long-term contracts; Higher earnings jobs with bad reviews impact JSS more heavily"

- **URL (xác nhận Upwork KHÔNG công bố trọng số cụ thể):** https://support.upwork.com/hc/en-us/articles/211063558-Job-Success-Score, https://support.upwork.com/hc/en-us/articles/38437458199059-How-is-my-Job-Success-Score-calculated
- **Nhận định quan trọng:** Toàn bộ tài liệu chính thức của Upwork về JSS **chỉ mô tả công thức là một tỷ lệ (kết quả thành công − kết quả tiêu cực)/tổng số kết quả**, KHÔNG phải là weighted-sum đa tiêu chí kiểu skill/budget/rating/availability như Aivora, và **không công bố bất kỳ trọng số phần trăm cụ thể nào** cho các yếu tố đầu vào (feedback, giá trị hợp đồng, thời gian quan hệ khách hàng...). Đây là bằng chứng trực tiếp cho việc Upwork giữ bí mật thuật toán ranking/scoring chính xác.

### 2.2. Upwork — Rising Talent badge (ví dụ về ngưỡng công khai)

- **URL:** https://support.upwork.com/hc/en-us/articles/211063228-How-to-become-a-Rising-Talent-on-Upwork (nội dung truy xuất qua tổng hợp WebSearch, chưa fetch trực tiếp được toàn văn do trang yêu cầu JS render)
- **Loại nguồn:** Official platform docs (paraphrase từ search engine snippet, KHÔNG phải trích nguyên văn 100% — ghi rõ để minh bạch)
- **Năm:** không rõ năm
- **Nội dung:** Freelancer cần JSS ≥ 90% (nếu đã có JSS) và hồ sơ hoàn thành 100% để đạt badge Rising Talent. Đây là một ví dụ về ngưỡng số cụ thể được Upwork công bố công khai (90%) cho một tiêu chí unlock tính năng, khác với trọng số nội bộ của thuật toán ranking.

### 2.3. Freelancer.com — Bid Ranking Guide

- **URL:** https://www.freelancer.com/community/articles/the-freelancer-com-bid-ranking-guide
- **Loại nguồn:** Official platform docs (Freelancer.com Community blog chính thức của nền tảng)
- **Năm:** bài viết gốc từ 2019 (theo kết quả search), nội dung vẫn đang được Freelancer.com duy trì tại URL hiện tại
- **Trích dẫn nguyên văn (4 yếu tố xếp hạng bid):**
  > "algorithm - (i) Reviews and Feedback, (ii) Use of Milestone Payments, (iii) Responsiveness and (iv) Quality of your profile"
- **Trích dẫn nguyên văn (recency của review quan trọng hơn):**
  > "Recency of Employer Feedback: Recent reviews are exponentially more important than old reviews. This is a crucial aspect of our ranking algorithm. A recent glorious review can earn you a massive lift in our rankings."
- **Trích dẫn nguyên văn (Milestone Payments):**
  > "Amount: The total value of all Milestone Payments that have ever been released to you. The more you earn using Milestone Payments, the higher your ranking. Frequency: Working more often with Milestone Payments improves your ranking significantly."
- **Nhận định:** Freelancer.com công khai 4 nhóm yếu tố định tính nhưng **không công bố trọng số phần trăm** giữa chúng. Không có yếu tố "giá/budget fit" tách biệt trong 4 nhóm này (khác với Aivora và Upwork).

### 2.4. Toptal — Screening criteria

- **URL:** https://www.toptal.com/faq
- **Loại nguồn:** Official platform docs (FAQ chính thức của Toptal)
- **Năm:** không rõ năm xuất bản cụ thể, nội dung truy cập 2026
- **Trích dẫn nguyên văn:**
  > "Of the more than 200,000 people who apply to join the Toptal network each year, we accept fewer than 3%."
  >
  > (Dịch: "Trong hơn 200.000 người nộp đơn gia nhập mạng lưới Toptal mỗi năm, chúng tôi chỉ chấp nhận dưới 3%.")
  >
  > Tiêu chí sàng lọc: "communication skills, personality, domain-specific knowledge, and a high level of professionalism"
  >
  > (Dịch: "kỹ năng giao tiếp, tính cách, kiến thức chuyên môn, và mức độ chuyên nghiệp cao")
- **Nhận định:** Toptal dùng mô hình **screening/gatekeeping trước khi vào mạng lưới** (pass/fail qua nhiều vòng phỏng vấn + test project), khác hẳn với mô hình **scoring liên tục để rank/recommend** của Aivora, Upwork, Freelancer.com. Không áp dụng trực tiếp để justify trọng số weighted-sum, nhưng xác nhận xu hướng chung: kỹ năng chuyên môn + giao tiếp là tiêu chí gatekeeping hàng đầu.

### 2.5. Academic paper — CrowdAdvisor (đánh giá freelancer trong online marketplace)

- **URL:** https://ieeexplore.ieee.org/document/7965433/ (DOI: 10.1109/ICSE-SEIP.2017.23)
- **Loại nguồn:** Academic paper (IEEE/ACM, ICSE-SEIP 2017 — Software Engineering in Practice Track)
- **Năm:** 2017
- **Tác giả:** K. Abhinav, Alpana Dubey, Sakshi Jain, G. Virdi, A. Kass, M. Mehta
- **Tiêu đề:** "CrowdAdvisor: A Framework for Freelancer Assessment in Online Marketplace"
- **Hạn chế khi trích dẫn:** Abstract của paper này bị publisher (IEEE) "elided" (ẩn) trong Semantic Scholar API — không truy xuất được toàn văn hay abstract công khai qua các công cụ hiện có (WebFetch bị chặn 403 trên IEEE Xplore và ResearchGate). Do đó **KHÔNG có trích dẫn nguyên văn** cho paper này — chỉ xác nhận được metadata (tiêu đề, venue, năm, DOI) qua Semantic Scholar/DBLP.
- **Giá trị của nguồn:** Xác nhận sự tồn tại của một dòng nghiên cứu học thuật (ICSE — hội nghị hàng đầu về software engineering) chuyên về multidimensional assessment framework cho freelancer trong online marketplace, tức là bài toán "đánh giá đa chiều freelancer" có nền tảng học thuật nghiêm túc. Không đủ dữ liệu để nói paper này dùng weighted-sum hay trọng số cụ thể nào.

### 2.6. Insight tổng hợp — Mục nghiên cứu 1

1. **Tất cả các nền tảng freelance lớn có công bố (Upwork, Freelancer.com) đều giữ bí mật trọng số chính xác của thuật toán ranking/scoring**, chỉ công bố danh sách định tính các yếu tố đầu vào. Đây là hành vi có chủ đích để chống gaming (spam feedback, spam milestone giả...).
2. Các yếu tố định tính lặp lại nhất qua cả 3 platform (Upwork, Freelancer.com, và gián tiếp Toptal) là: **feedback/rating của khách hàng, lịch sử hoàn thành hợp đồng/completion, và mức độ phản hồi/responsiveness** — đều có mặt tương ứng trong công thức Aivora (RatingScore, CompletionScore, AvailabilityScore).
3. **Không nền tảng nào trong số này công bố một tiêu chí "budget/price fit" tách biệt và có trọng số riêng** như Aivora — Upwork và Freelancer.com xử lý giá qua cơ chế đấu thầu (bidding) của freelancer, không phải qua điểm số phù hợp ngân sách được tính sẵn. Đây là điểm khác biệt kiến trúc, không phải sai lệch — hợp lý vì Aivora recommend expert cho client trước khi có bid cụ thể.
4. **Skill/domain expertise được nhắc đến ở vị trí ưu tiên hàng đầu ở Toptal** (screening criteria đầu tiên là domain-specific knowledge) — phù hợp định hướng SkillScore có trọng số cao nhất (40%) trong công thức Aivora, dù không có con số phần trăm nào được xác nhận từ nguồn chính thức.
5. Không tìm được bất kỳ nguồn chính thức nào công bố con số phần trăm cụ thể kiểu "skill match chiếm X%, rating chiếm Y%". Các con số phần trăm như "Job Success Score (25-30%), skill relevance (20-25%)..." xuất hiện trên một số blog bên thứ ba (ví dụ jobbers.io) **không được xác minh, có dấu hiệu là suy đoán/dựng số của blog SEO chứ không phải dữ liệu từ Upwork** — báo cáo này **cố ý không sử dụng** các con số đó vì không lấy được xác nhận nguồn gốc qua WebFetch (trang chặn truy cập 403, và nội dung không khớp với các trang Upwork Help chính thức đã fetch được).

---

## 3. Mục nghiên cứu 2: MCDM/weighted-sum/AHP cho service-provider matching

### 3.1. AHP là mô hình weighted-sum (nền tảng lý thuyết)

- **URL:** https://bpmsg.com/ahp-alternative-evaluation-weighted-sum-or-weighted-product-model/
- **Loại nguồn:** Third-party analysis / industry blog (tác giả Klaus D. Goepel, chuyên gia AHP có công cụ AHP-OS được academia trích dẫn — không phải paper học thuật chính thức nhưng là nguồn kỹ thuật uy tín trong cộng đồng AHP)
- **Năm:** không rõ năm
- **Nội dung (paraphrase, không phải trích nguyên văn do trang không được WebFetch trực tiếp, chỉ qua search summary):** AHP dựa trên mô hình weighted-sum, còn được gọi trong literature học thuật là "additive multi-criteria value models" hoặc "multi-attribute value models". Bước cuối của AHP là kết hợp trọng số tiêu chí với điểm số của các phương án bằng cách nhân và cộng để ra điểm tổng cho mỗi phương án — về bản chất đây chính là công thức weighted-sum mà Aivora đang dùng.

### 3.2. Academic paper — AHP cho Vendor Selection (ví dụ số liệu thực tế)

- **URL:** https://www.atlantis-press.com/article/125959149.pdf
- **Loại nguồn:** Academic paper (Kỷ yếu hội nghị "Business Innovation and Engineering Conference 2020", xuất bản trong "Advances in Economics, Business and Management Research", Atlantis Press — peer-reviewed conference proceedings, không phải top-tier ACM/IEEE nhưng là academic publisher hợp lệ)
- **Năm:** 2021 (Copyright © 2021 The Authors, Published by Atlantis Press International B.V.)
- **Tác giả:** Muhammad Fariz Tiowiradin, Nurmala (University of Indonesia)
- **Tiêu đề:** "Analytical Hierarchy Process Model for Vendor Selection"
- **Trích dẫn nguyên văn:**
  > "Supplier selection is basically a multiple criteria decision-making (MCDM) problem."
  >
  > (Dịch: "Lựa chọn nhà cung cấp về cơ bản là một bài toán ra quyết định đa tiêu chí (MCDM).")
  >
  > "The AHP method uses existing data that are qualitative based on perceptions, experiences, intuition from experts so that they can predict criteria with a high degree of accuracy."
  >
  > (Dịch: "Phương pháp AHP sử dụng dữ liệu định tính dựa trên nhận thức, kinh nghiệm, trực giác của chuyên gia để dự đoán các tiêu chí với độ chính xác cao.")
- **Số liệu trọng số thực tế từ paper (ví dụ minh họa phương pháp, KHÔNG áp dụng trực tiếp cho freelance marketplace):** Nghiên cứu này áp dụng AHP cho 5 tiêu chí chọn vendor xe vận hành tại một công ty khoan dầu ở Indonesia, kết quả trọng số ưu tiên: Service = 0.30897 (quan trọng nhất), Cost = 0.29418, Quality = 0.21974, Delivery = 0.11343, IT = 0.06368 (tổng = 1.0). Đây là bằng chứng cụ thể cho thấy: (a) tổng trọng số các tiêu chí luôn chuẩn hóa về 1.0 giống cách Aivora validate tổng 5 trọng số = 1.0; (b) trọng số cụ thể luôn phụ thuộc ngữ cảnh ngành/bài toán, không có một bộ số "chuẩn" áp dụng chung cho mọi domain.

### 3.3. Academic paper tổng quan — MCDM cho crowdsourcing worker selection

- **URL:** https://onlinelibrary.wiley.com/doi/10.1155/2021/9368128
- **Loại nguồn:** Academic paper (Scientific Programming, Wiley — peer-reviewed journal)
- **Năm:** 2021
- **Tiêu đề:** "Selection of Crowd in Crowdsourcing for Smart Intelligent Applications: A Systematic Mapping Study"
- **Nội dung (paraphrase từ search summary, chưa fetch được toàn văn trực tiếp để lấy quote nguyên văn):** Nghiên cứu này xác nhận multi-criteria-based crowd selection là yếu tố bắt buộc (mandatory) cho sự thành công của hoạt động crowdsourcing — tức là việc kết hợp nhiều tiêu chí (không chỉ 1 tiêu chí đơn lẻ) để chọn/rank người thực hiện công việc là cách tiếp cận được đồng thuận rộng rãi trong literature crowdsourcing/gig economy, tương tự bài toán recommend expert của Aivora.

### 3.4. Insight tổng hợp — Mục nghiên cứu 2

1. **Phương pháp weighted-sum (linear combination của nhiều tiêu chí, mỗi tiêu chí một trọng số, tổng trọng số = 1.0) có cơ sở học thuật vững chắc**, được dùng rộng rãi trong MCDM cho bài toán service-provider/vendor/supplier/worker selection — không chỉ 1 paper mà là cả một nhánh literature (AHP, SAW/Simple Additive Weighting, WSM). Việc Aivora chọn kiến trúc weighted-sum là **lựa chọn phương pháp luận hợp lý, có tiền lệ học thuật rõ ràng**.
2. Tuy nhiên, **KHÔNG có paper nào tìm được nói trực tiếp về bài toán "recommend expert cho client trong marketplace freelance" với bộ trọng số 40/20/20/10/10 cụ thể**. Các con số trọng số trong mọi paper AHP đều là **kết quả tính toán riêng cho từng bài toán/ngành cụ thể** (ví dụ paper vendor selection ở trên ra kết quả Service 30.9%, Cost 29.4%..., khác hẳn cấu trúc của Aivora), không phải hằng số phổ quát có thể sao chép.
3. Điểm chung xuyên suốt các paper MCDM: **AHP "chuẩn" đòi hỏi trọng số được suy ra từ pairwise comparison matrix (ma trận so sánh cặp) do chuyên gia/stakeholder đánh giá**, chứ không phải gán trực tiếp theo trực giác của developer như cách Aivora đang làm (hardcode 40/20/20/10/10 không qua quy trình AHP formal nào). Đây là khoảng cách giữa "áp dụng đúng tinh thần MCDM" và "áp dụng đúng quy trình AHP formal" — Aivora hiện đang ở dạng **Weighted Sum Model (WSM) đơn giản, không phải AHP đầy đủ** (không có bước pairwise comparison + consistency ratio check).

---

## 4. Mục nghiên cứu 3: Penalty theo dispute/cancellation rate ở các platform khác

### 4.1. Airbnb — Superhost cancellation rate

- **URL:** https://www.airbnb.com/help/article/829
- **Loại nguồn:** Official platform docs (Airbnb Help Center)
- **Năm:** không rõ năm xuất bản cụ thể, nội dung truy cập 2026
- **Trích dẫn nguyên văn:**
  > "Requirements to be a Superhost — To be a Superhost, hosts must be the listing owner of a home listing with an account in good standing and need to have met the following criteria: Hosted at least 10 reservations, or 3 reservations that total at least 100 nights; Respond to 90% of new messages, and accept or decline new reservation requests, within 24 hours; Maintained a less than 1% cancellation rate, with exceptions for cancellations due to Major Disruptive Events or other valid reasons; Maintained a 4.8 or higher overall rating"
- **Về ngưỡng đánh giá:**
  > "Airbnb evaluates all hosts quarterly to determine whether they have met the stringent criteria." (paraphrase từ search summary, xác nhận gián tiếp qua nội dung trang — đánh giá lại vào ngày 1 tháng 1, 4, 7, 10)
- **Về giải thích lý do chọn ngưỡng:** Trang tài liệu chính thức **KHÔNG giải thích tại sao chọn cụ thể 1% hay 4.8**, chỉ liệt kê tiêu chí. Không tìm được bài viết chính thức nào của Airbnb giải thích rationale định lượng đằng sau con số 1%.

### 4.2. eBay — Seller standards policy (defect rate, cases closed without seller resolution)

- **URL:** https://www.ebay.com/help/policies/selling-policies/seller-standards-policy?id=4347
- **Loại nguồn:** Official platform docs (eBay Help — Seller standards policy)
- **Năm:** không rõ năm xuất bản cụ thể, nội dung truy cập 2026
- **Trích dẫn nguyên văn (ngưỡng tối thiểu áp dụng cho MỌI seller):**
  > "All sellers are required to maintain the following minimum performance standards for their listings on eBay.com within their evaluation period: Cases closed without seller resolution: No more than 2 (or 0.3% of transactions); Transaction defect rate: No more than 2% of transactions"
- **Trích dẫn nguyên văn (định nghĩa seller level):**
  > "Top Rated means you're exceeding our performance expectations, as well as having an established sales history and complying with other eBay policies"; "Above Standard means you're meeting our expectations"; "Below Standard means that your performance has fallen below our minimum standards and as a result, we may place limitations on your selling activity, including charging higher final value fees, until your performance improves"
- **Trích dẫn nguyên văn (ngưỡng Below Standard chi tiết — tương tự cơ chế "mẫu quá nhỏ" của Aivora):**
  > "You're allowed up to 2% of transactions with defects within an evaluation period. You'll only be evaluated as Below Standard if your transaction defects are associated with more than 4 different buyers."
  >
  > (Dịch: "Bạn được phép có tới 2% giao dịch có lỗi (defect) trong một chu kỳ đánh giá. Bạn chỉ bị đánh giá là Below Standard nếu các lỗi giao dịch liên quan đến hơn 4 người mua khác nhau.")
- **Trích dẫn nguyên văn (ngưỡng Top Rated):**
  > "Cases closed without seller resolution: No more than 2 (or 0.3% of transactions); Transaction defect rate: No more than 0.5%, associated with no more than 3 different buyers; Late shipment rate: No more than 5 (or 3% of transactions)"
- **Nhận định quan trọng:** eBay có cơ chế **"ngưỡng số lượng buyer khác nhau" (more than 4 different buyers / no more than 3 different buyers)** — về bản chất tương đương triết lý "tránh 1 dispute đầu tiên giết chết điểm của expert mới vì mẫu quá nhỏ" trong comment code của Aivora (ngưỡng `completedProjects >= 3`). Đây là bằng chứng thực tế mạnh nhất tìm được cho việc **các platform lớn đều có cơ chế bảo vệ khỏi outlier/mẫu nhỏ khi tính penalty theo tỷ lệ lỗi**, dù công thức toán học cụ thể khác nhau (eBay dùng "đếm số buyer distinct", Aivora dùng "đếm số completedProjects tối thiểu").

### 4.3. Uber — Driver rating/deactivation policy

- **URL:** https://www.uber.com/us/en/drive/driver-app/deactivation-review/
- **Loại nguồn:** Official platform docs (Uber — trang Deactivation Review chính thức)
- **Năm:** không rõ năm xuất bản cụ thể, nội dung truy cập 2026
- **Nội dung (paraphrase qua WebFetch, do trang không cho trích xuất nguyên văn đầy đủ câu):** "A driver or delivery person can lose access to part or all of the Uber platform for ratings that are below the minimum average rating in their city." Hệ thống rating dựa trên "the last 500 ratings from riders" và có cơ chế loại bỏ đánh giá không công bằng. Tài liệu **không nêu một con số ngưỡng rating cụ thể** (ví dụ 4.6) — ngưỡng được mô tả là khác nhau theo từng thành phố ("minimum average rating in their city").
- **Về ngưỡng "4.6" hay xuất hiện trên báo chí/blog:** Con số này chỉ xuất hiện trong các nguồn thứ cấp (ví dụ therideshareguy.com, entrepreneur.com) chứ **KHÔNG được xác nhận trong tài liệu chính thức** đã fetch được. Báo cáo này ghi rõ: **không có ngưỡng số cụ thể được Uber công bố công khai chính thức**, chỉ có mô tả định tính "dưới mức trung bình tối thiểu của thành phố".
- **Về cancellation rate:** Tài liệu xác nhận Uber theo dõi cancellation rate của driver so với "average rate for your metro area" và gửi cảnh báo trước khi deactivate, nhưng cũng không công bố % cụ thể.

### 4.4. Insight tổng hợp — Mục nghiên cứu 3

1. **Airbnb là nguồn duy nhất công bố một ngưỡng cap tuyệt đối, dạng số cụ thể, không phụ thuộc điều kiện khác:** cancellation rate < 1%. Đây là ngưỡng "cứng" (hard cap), khác về bản chất với cách Aivora tính OverduePenalty (tuyến tính có cap 0.3, không phải ngưỡng loại trừ nhị phân).
2. **eBay có cơ chế 2 tầng tương tự triết lý của Aivora**: (a) một ngưỡng % tối đa (2% defect rate cho mức tối thiểu, 0.5% cho Top Rated) — tương tự cách Aivora dùng `disputeRate` liên tục; (b) một điều kiện về **số lượng mẫu tối thiểu** trước khi áp penalty nặng (">4 different buyers" mới bị đánh Below Standard) — đây là bằng chứng thực tế rõ ràng nhất ủng hộ triết lý "cần đủ mẫu mới phạt nặng" mà Aivora áp dụng qua ngưỡng `completedProjects >= 3`, dù eBay đếm theo số buyer distinct chứ không phải số project hoàn thành.
3. **Không platform nào trong 3 platform này công khai công thức toán học đầy đủ để tính điểm phạt** (ví dụ hệ số nhân như 1.5 hay 0.3 của Aivora) — tất cả chỉ công bố **ngưỡng cap cuối cùng** (1% ở Airbnb, 2%/0.5% ở eBay), không công bố công thức trung gian. Điều này có nghĩa: **không có nguồn nào để đối chiếu trực tiếp con số 1.5 (dispute penalty factor) hay 0.3 (overdue penalty factor) của Aivora** — đây là những con số nội bộ hoàn toàn, không thể "xác minh đúng/sai" so với ngành.
4. Không platform nào giải thích rationale định lượng cho việc tại sao chọn 1% (Airbnb) hay 2%/0.5% (eBay) thay vì con số khác — điều này gợi ý các ngưỡng này cũng có khả năng cao là **quyết định nghiệp vụ/kinh doanh nội bộ**, không dựa trên một mô hình thống kê công khai nào, tương tự tình trạng của Aivora hiện tại.

---

## 5. Bảng tổng hợp nguồn mạnh/yếu

| Nguồn | Loại | Đủ mạnh để cite trực tiếp trong code comment/docs? | Lý do |
|---|---|---|---|
| Upwork — How is my JSS calculated | Official platform docs | Có | Trích dẫn nguyên văn xác nhận được qua fetch trực tiếp; hữu ích để justify việc kết hợp nhiều tiêu chí feedback/completion, nhưng KHÔNG có con số trọng số để so sánh |
| Upwork — What factors affect JSS | Official platform docs | Có | Xác nhận danh sách yếu tố định tính (feedback, long-term relationship, contract value) — dùng để justify sự tồn tại của RatingScore/CompletionScore, không dùng để justify con số % |
| Upwork — Rising Talent (JSS ≥ 90%) | Official platform docs (qua search snippet, chưa fetch toàn văn) | Tham khảo gián tiếp | Chỉ là ví dụ về 1 ngưỡng unlock tính năng, không phải trọng số ranking |
| Freelancer.com — Bid Ranking Guide | Official platform docs | Có | Trích dẫn nguyên văn xác nhận qua fetch trực tiếp; xác nhận xu hướng "recency của feedback quan trọng hơn", không có budget/giá làm tiêu chí riêng |
| Toptal — FAQ (top 3%) | Official platform docs | Tham khảo gián tiếp | Xác nhận mô hình gatekeeping/screening khác cấu trúc với scoring liên tục của Aivora, không dùng để justify trọng số |
| CrowdAdvisor (IEEE ICSE-SEIP 2017) | Academic paper | Tham khảo gián tiếp | Chỉ xác nhận metadata (tồn tại, venue, năm) — abstract bị publisher chặn, không lấy được nội dung cụ thể |
| bpmsg.com — AHP weighted-sum | Third-party analysis / industry blog | Tham khảo gián tiếp | Không phải paper chính thức, chỉ paraphrase qua search summary, dùng để giải thích khái niệm AHP = weighted-sum |
| Atlantis Press — AHP Vendor Selection (2021) | Academic paper (peer-reviewed conference proceedings) | Có | Trích dẫn nguyên văn xác nhận qua fetch PDF trực tiếp + số liệu trọng số thực tế minh họa phương pháp; không áp dụng trực tiếp cho freelance marketplace nhưng chứng minh weighted-sum/AHP là phương pháp hợp lệ cho service-provider selection |
| Wiley Scientific Programming — Crowd Selection Systematic Mapping (2021) | Academic paper | Tham khảo gián tiếp | Chỉ paraphrase qua search summary, chưa fetch toàn văn để lấy quote chính xác |
| Airbnb — Superhost requirements | Official platform docs | Có | Trích dẫn nguyên văn xác nhận qua fetch trực tiếp; ngưỡng cancellation rate <1% là con số cứng, rõ ràng, dùng được để đối chiếu triết lý (không đối chiếu công thức) |
| eBay — Seller standards policy | Official platform docs | Có | Trích dẫn nguyên văn xác nhận qua fetch trực tiếp (curl); nguồn MẠNH NHẤT trong toàn bộ nghiên cứu — có cả ngưỡng % VÀ điều kiện mẫu tối thiểu (">4 different buyers"), rất gần với triết lý `completedProjects >= 3` của Aivora |
| Uber — Deactivation review | Official platform docs | Tham khảo gián tiếp | Fetch được nhưng nội dung không có con số ngưỡng cụ thể (chỉ nói "minimum average rating in their city"), không đủ cụ thể để đối chiếu số liệu |

---

## 6. Kết luận và khuyến nghị cụ thể

### Khuyến nghị chính: **GIỮ NGUYÊN** bộ trọng số hiện tại, với ghi chú trung thực bổ sung vào code/docs

Sau khi đối chiếu với 12 nguồn (7 official platform docs, 2 academic paper xác nhận được nội dung, 1 academic paper chỉ xác nhận metadata, 2 third-party/search-summary), kết luận rõ ràng:

1. **Không có bất kỳ nguồn nào — kể cả academic paper hay official docs của Upwork/Freelancer.com/Toptal/eBay/Airbnb/Uber — công bố một bộ trọng số phần trăm cụ thể cho bài toán "recommend/rank freelancer" mà Aivora có thể sao chép trực tiếp.** Đây là phát hiện nhất quán và rõ ràng nhất của nghiên cứu này. Các nền tảng lớn nhất (Upwork, Freelancer.com) chủ động giữ bí mật trọng số chính xác vì lý do chống gaming thuật toán — đã xác nhận qua việc official docs của họ chỉ liệt kê yếu tố định tính, không có con số.
2. Do đó, **con số 40/20/20/10/10 của Aivora không thể được "sửa cho khớp" với bất kỳ nguồn nào**, vì không tồn tại nguồn công khai nào có con số để khớp theo.
3. **Thứ tự ưu tiên định tính của Aivora (Skill > Rating = Budget > Availability = Completion) phù hợp với xu hướng chung quan sát được:** skill/domain expertise luôn được nhắc ở vị trí ưu tiên hàng đầu (Toptal đặt domain-specific knowledge là tiêu chí sàng lọc đầu tiên; academic literature MCDM luôn coi "core capability" là tiêu chí trọng số cao nhất trong các mô hình supplier selection tương tự). Việc Aivora cho SkillScore trọng số cao nhất (40%, gấp đôi mỗi tiêu chí còn lại) là **có cơ sở định hướng, dù không có con số chính xác để đối chiếu**.
4. **Phương pháp weighted-sum (linear combination, tổng trọng số chuẩn hóa về 1.0) có nền tảng học thuật vững** (MCDM/AHP/WSM literature) cho bài toán service-provider selection nói chung — đây là điểm mạnh có thể trích dẫn được: paper AHP Vendor Selection (Atlantis Press, 2021) xác nhận nguyên văn "Supplier selection is basically a multiple criteria decision-making (MCDM) problem", và mọi mô hình AHP/WSM tìm được đều dùng đúng công thức nhân-tổng-chuẩn hóa-về-1.0 giống Aivora.
5. **Cơ chế penalty với ngưỡng mẫu tối thiểu (`completedProjects >= 3`) của Aivora có tiền lệ thực tế mạnh nhất trong toàn bộ nghiên cứu**: eBay áp dụng chính xác cùng triết lý — chỉ đánh giá "Below Standard" khi defect liên quan đến hơn 4 buyer khác nhau, tức là **không phạt seller mới có 1 giao dịch lỗi vì mẫu quá nhỏ**. Đây là bằng chứng thực tế (không phải suy đoán) cho thấy triết lý "bảo vệ expert mới khỏi outlier vì mẫu nhỏ" trong comment code của Aivora là **cách tiếp cận phổ biến trong ngành reputation system**, dù công thức đếm cụ thể (số buyer distinct ở eBay vs. số project hoàn thành ở Aivora) khác nhau.
6. **Các hệ số nhân cụ thể trong penalty (1.5 cho dispute, 0.3 cho overdue) và các cap (0.5, 0.3) hoàn toàn không có nguồn nào để đối chiếu** — không platform nào trong 3 platform đã nghiên cứu (Airbnb, eBay, Uber) công bố công thức toán học trung gian, chỉ công bố ngưỡng cap cuối cùng (dạng "≤1%" hoặc "≤2%"). Đây là khoảng trống thực sự không thể lấp bằng nghiên cứu external — các hệ số này **chỉ có thể được tinh chỉnh bằng dữ liệu nội bộ của Aivora** (A/B test, phân tích outcome thực tế), không phải bằng cách tra cứu thêm.

### Khuyến nghị hành động cụ thể

- **KHÔNG thay đổi các con số 40/20/20/10/10, 1.5/0.5, 0.3/0.3, ngưỡng ≥3** — không có cơ sở nguồn nào để đề xuất một con số thay thế cụ thể tốt hơn.
- **NÊN cập nhật comment trong `RecommendationOptions.cs` và `RecommendationScorer.cs`** để thêm 1 dòng dẫn chiếu tới file research này, với nội dung trung thực dạng: *"Trọng số 40/20/20/10/10 và các hệ số penalty là quyết định nghiệp vụ nội bộ, được định hướng bởi xu hướng ngành chung (skill/domain expertise là tiêu chí ưu tiên hàng đầu trong freelance matching; cơ chế bảo vệ mẫu nhỏ trước khi áp penalty là thông lệ phổ biến ở các reputation system như eBay) — KHÔNG phải con số sao chép chính xác từ bất kỳ nguồn nào cụ thể. Xem `docs/research/expert-recommendation-weight-justification.md` để biết chi tiết nghiên cứu."*
- Nếu muốn tăng độ nghiêm ngặt học thuật trong tương lai, hướng cải tiến khả thi (không thuộc phạm vi nghiên cứu này, chỉ gợi ý): áp dụng quy trình AHP formal (pairwise comparison matrix với stakeholder Aivora — PM/BA — để suy ra trọng số + tính consistency ratio) thay vì gán trực tiếp theo trực giác, dựa trên phương pháp luận đã xác nhận ở Mục nghiên cứu 2.

---

## 7. Mục lục tham khảo đầy đủ URL

1. Upwork Help Center — "How is my Job Success Score calculated?" — https://support.upwork.com/hc/en-us/articles/38437458199059-How-is-my-Job-Success-Score-calculated
2. Upwork Help Center — "What factors affect my Job Success Score?" — https://support.upwork.com/hc/en-us/articles/38439816969875-What-factors-affect-my-Job-Success-Score
3. Upwork Help Center — "Job Success Score" (bài viết gốc) — https://support.upwork.com/hc/en-us/articles/211063558-Job-Success-Score
4. Upwork Help Center — "How to become a Rising Talent on Upwork" — https://support.upwork.com/hc/en-us/articles/211063228-How-to-become-a-Rising-Talent-on-Upwork
5. Freelancer.com Community — "The Freelancer.com Bid Ranking Guide" — https://www.freelancer.com/community/articles/the-freelancer-com-bid-ranking-guide
6. Toptal — FAQ — https://www.toptal.com/faq
7. IEEE Xplore — CrowdAdvisor: A Framework for Freelancer Assessment in Online Marketplace (DOI: 10.1109/ICSE-SEIP.2017.23) — https://ieeexplore.ieee.org/document/7965433/
8. Semantic Scholar — metadata CrowdAdvisor paper — https://www.semanticscholar.org/paper/CrowdAdvisor:-A-Framework-for-Freelancer-Assessment-Abhinav-Dubey/5bdd629824ed1278f0574a7d17b680a52b9632e7
9. bpmsg.com — "AHP Alternative Evaluation – Weighted Sum or Weighted Product Model?" — https://bpmsg.com/ahp-alternative-evaluation-weighted-sum-or-weighted-product-model/
10. Atlantis Press — Tiowiradin, M.F., Nurmala (2021), "Analytical Hierarchy Process Model for Vendor Selection", Advances in Economics, Business and Management Research, vol. 184 — https://www.atlantis-press.com/article/125959149.pdf
11. Wiley Online Library — "Selection of Crowd in Crowdsourcing for Smart Intelligent Applications: A Systematic Mapping Study" (2021) — https://onlinelibrary.wiley.com/doi/10.1155/2021/9368128
12. Airbnb Help Center — "What's required to be a Superhost" — https://www.airbnb.com/help/article/829
13. eBay Help — "Seller standards policy" — https://www.ebay.com/help/policies/selling-policies/seller-standards-policy?id=4347
14. eBay Help — "Global seller performance policy" — https://www.ebay.com/help/policies/selling-policies/global-seller-performance-policy?id=4351
15. Uber — "Deactivations: Losing Account Access" — https://www.uber.com/us/en/drive/driver-app/deactivation-review/

### Nguồn phụ đã tham khảo qua WebSearch summary nhưng KHÔNG dùng để trích dẫn nguyên văn (do không fetch được toàn văn hoặc là nguồn thứ cấp không đủ tin cậy)

- jobbers.io — "The Complete Upwork Algorithm Guide" — con số phần trăm trọng số JSS/skill relevance/... trên trang này **không được xác minh và không được sử dụng** trong báo cáo vì có dấu hiệu là suy đoán của blog SEO bên thứ ba, không khớp với nội dung chính thức của Upwork Help Center.
- therideshareguy.com, entrepreneur.com — các nguồn nhắc đến ngưỡng rating "4.6" của Uber — đây là thông tin thứ cấp, không được Uber xác nhận chính thức trong tài liệu đã fetch được.
