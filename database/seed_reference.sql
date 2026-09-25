-- Reference data for HeatingSystems (bilingual: English / Ukrainian display names).
-- IMPORTANT: climate data, tariffs and emission factors are reference values for preliminary design.
-- Verify them against the cited documents (and current tariffs) before use in official design documentation.
-- All values can be edited in the application ("Reference data" page) or directly in the database.

-- ------------------------------------------------------------------ materials (λ, W/(m·K))
INSERT INTO materials (id, name_en, name_uk, category, conductivity, density, source) VALUES
 (1,  'Solid clay brick',                         'Цегла керамічна повнотіла',                     1, 0.81,  1800, 'DSTU B V.2.6-189:2013, operating conditions B'),
 (2,  'Hollow clay brick',                        'Цегла керамічна пустотіла',                     1, 0.58,  1400, 'DSTU B V.2.6-189:2013, operating conditions B'),
 (3,  'Sand-lime brick',                          'Цегла силікатна',                               1, 0.87,  1800, 'DSTU B V.2.6-189:2013, operating conditions B'),
 (4,  'Porous clay block',                        'Керамічний поризований блок',                   1, 0.21,   800, 'Typical declared values of manufacturers (EN 1745)'),
 (5,  'Aerated concrete D400',                    'Газобетон D400',                                1, 0.13,   400, 'DSTU B V.2.7-137:2008, moisture content 5 %'),
 (6,  'Aerated concrete D500',                    'Газобетон D500',                                1, 0.16,   500, 'DSTU B V.2.7-137:2008, moisture content 5 %'),
 (7,  'Expanded clay concrete block',             'Керамзитобетонний блок',                        1, 0.41,  1000, 'DSTU B V.2.6-189:2013, operating conditions B'),
 (8,  'Cinder concrete block',                    'Шлакобетонний блок',                            1, 0.60,  1400, 'DSTU B V.2.6-189:2013, operating conditions B'),
 (9,  'Reinforced concrete',                      'Залізобетон',                                   2, 2.04,  2500, 'DSTU B V.2.6-189:2013, operating conditions B'),
 (10, 'Gravel concrete',                          'Бетон на гравії',                               2, 1.86,  2400, 'DSTU B V.2.6-189:2013, operating conditions B'),
 (11, 'Softwood (across the grain)',              'Деревина хвойна (поперек волокон)',             3, 0.18,   500, 'DSTU B V.2.6-189:2013, operating conditions B'),
 (12, 'Glued laminated timber / CLT',             'Клеєний брус / CLT',                            3, 0.13,   500, 'EN ISO 10456:2007, table 3'),
 (13, 'OSB board',                                'Плита OSB',                                     3, 0.13,   650, 'EN ISO 10456:2007, table 3'),
 (14, 'Expanded polystyrene EPS',                 'Пінополістирол EPS',                            4, 0.040,   20, 'Typical declared values (EN 13163)'),
 (15, 'Graphite EPS',                             'Пінополістирол EPS з графітом',                 4, 0.032,   18, 'Typical declared values (EN 13163)'),
 (16, 'Extruded polystyrene XPS',                 'Екструдований пінополістирол XPS',              4, 0.034,   35, 'Typical declared values (EN 13164)'),
 (17, 'Stone wool, facade slab',                  'Мінеральна вата (кам''яна), фасадна',           4, 0.039,  140, 'Typical declared values (EN 13162)'),
 (18, 'Glass wool',                               'Мінеральна вата (скловолокно)',                 4, 0.037,   15, 'Typical declared values (EN 13162)'),
 (19, 'Polyisocyanurate boards PIR',              'Поліізоціануратні плити PIR',                   4, 0.023,   32, 'Typical declared values (EN 13165)'),
 (20, 'Spray polyurethane foam (closed cell)',    'Пінополіуретан напилюваний (закритокомірковий)', 4, 0.028,  40, 'Typical declared values (EN 14315)'),
 (21, 'Cellulose fibre',                          'Целюлозна ековата',                             4, 0.041,   50, 'Typical declared values (EN 15101)'),
 (22, 'Expanded clay (loose fill)',               'Керамзит (засипка)',                            4, 0.16,   500, 'DSTU B V.2.6-189:2013, operating conditions B'),
 (23, 'Cement-sand plaster / screed',             'Штукатурка цементно-піщана / стяжка',           5, 0.93,  1800, 'DSTU B V.2.6-189:2013, operating conditions B'),
 (24, 'Lime-sand plaster',                        'Штукатурка вапняно-піщана',                     5, 0.81,  1600, 'DSTU B V.2.6-189:2013, operating conditions B'),
 (25, 'Gypsum plasterboard',                      'Гіпсокартон',                                   5, 0.25,   900, 'EN ISO 10456:2007, table 3');

-- ------------------------------------------------------------------ windows (U_w, W/(m²·K))
INSERT INTO window_types (id, name_en, name_uk, u_value, g_value, source) VALUES
 (1, 'Timber, single glazing',                          'Дерев''яні, одинарне скління',                         5.0, 0.85, 'Typical values, EN ISO 10077-1'),
 (2, 'Timber, coupled sashes (two panes)',              'Дерев''яні спарені (два скла)',                        2.6, 0.75, 'Typical values, EN ISO 10077-1'),
 (3, 'Timber, low-e insulating glass unit',             'Дерев''яні євровікна, енергозберігаючий склопакет',    1.3, 0.55, 'Typical values, EN ISO 10077-1'),
 (4, 'PVC, double glazing 4-16-4',                      'ПВХ, однокамерний склопакет 4-16-4',                   2.5, 0.75, 'Typical values, EN ISO 10077-1 / EN 673'),
 (5, 'PVC, double glazing low-e, argon',                'ПВХ, однокамерний енергозберігаючий (i-скло, аргон)',  1.3, 0.60, 'Typical values, EN ISO 10077-1 / EN 673'),
 (6, 'PVC, triple glazing 4-10-4-10-4',                 'ПВХ, двокамерний склопакет 4-10-4-10-4',               1.8, 0.70, 'Typical values, EN ISO 10077-1 / EN 673'),
 (7, 'PVC, triple glazing, 2× low-e, argon',            'ПВХ, двокамерний енергозберігаючий (2 i-скла, аргон)', 0.9, 0.50, 'Typical values, EN ISO 10077-1 / EN 673'),
 (8, 'Aluminium without thermal break',                 'Алюмінієві без терморозриву',                          3.5, 0.70, 'Typical values, EN ISO 10077-1'),
 (9, 'Aluminium with thermal break',                    'Алюмінієві з терморозривом',                           1.6, 0.60, 'Typical values, EN ISO 10077-1');

-- ------------------------------------------------------------------ climate (regional centres of Ukraine)
-- θ_e: coldest five-day period (probability 0.92); heating season: days with mean temperature ≤ 8 °C.
INSERT INTO climate_locations (id, city_en, city_uk, region_en, region_uk, design_temperature, heating_days, heating_mean_temperature, annual_mean_temperature, source) VALUES
 (1,  'Kyiv',            'Київ',             'Kyiv City',                     'м. Київ',                   -22, 176, -0.1,  7.7, 'DSTU-N B V.1.1-27:2010 (reference value, to be verified)'),
 (2,  'Vinnytsia',       'Вінниця',          'Vinnytsia Oblast',              'Вінницька',                 -21, 184, -0.6,  7.0, 'DSTU-N B V.1.1-27:2010 (reference value, to be verified)'),
 (3,  'Lutsk',           'Луцьк',            'Volyn Oblast',                  'Волинська',                 -20, 180,  0.2,  7.5, 'DSTU-N B V.1.1-27:2010 (reference value, to be verified)'),
 (4,  'Dnipro',          'Дніпро',           'Dnipropetrovsk Oblast',         'Дніпропетровська',          -23, 169, -0.4,  8.8, 'DSTU-N B V.1.1-27:2010 (reference value, to be verified)'),
 (5,  'Donetsk',         'Донецьк',          'Donetsk Oblast',                'Донецька',                  -23, 171, -1.3,  8.2, 'DSTU-N B V.1.1-27:2010 (reference value, to be verified)'),
 (6,  'Zhytomyr',        'Житомир',          'Zhytomyr Oblast',               'Житомирська',               -21, 183, -0.4,  7.1, 'DSTU-N B V.1.1-27:2010 (reference value, to be verified)'),
 (7,  'Uzhhorod',        'Ужгород',          'Zakarpattia Oblast',            'Закарпатська',              -18, 155,  1.8,  9.6, 'DSTU-N B V.1.1-27:2010 (reference value, to be verified)'),
 (8,  'Zaporizhzhia',    'Запоріжжя',        'Zaporizhzhia Oblast',           'Запорізька',                -22, 164,  0.0,  9.6, 'DSTU-N B V.1.1-27:2010 (reference value, to be verified)'),
 (9,  'Ivano-Frankivsk', 'Івано-Франківськ', 'Ivano-Frankivsk Oblast',        'Івано-Франківська',         -20, 183,  0.8,  7.2, 'DSTU-N B V.1.1-27:2010 (reference value, to be verified)'),
 (10, 'Kropyvnytskyi',   'Кропивницький',    'Kirovohrad Oblast',             'Кіровоградська',            -22, 175, -0.6,  8.0, 'DSTU-N B V.1.1-27:2010 (reference value, to be verified)'),
 (11, 'Luhansk',         'Луганськ',         'Luhansk Oblast',                'Луганська',                 -25, 169, -1.4,  8.5, 'DSTU-N B V.1.1-27:2010 (reference value, to be verified)'),
 (12, 'Lviv',            'Львів',            'Lviv Oblast',                   'Львівська',                 -19, 187,  0.9,  7.6, 'DSTU-N B V.1.1-27:2010 (reference value, to be verified)'),
 (13, 'Mykolaiv',        'Миколаїв',         'Mykolaiv Oblast',               'Миколаївська',              -20, 163,  0.9,  9.9, 'DSTU-N B V.1.1-27:2010 (reference value, to be verified)'),
 (14, 'Odesa',           'Одеса',            'Odesa Oblast',                  'Одеська',                   -18, 158,  1.8, 10.6, 'DSTU-N B V.1.1-27:2010 (reference value, to be verified)'),
 (15, 'Poltava',         'Полтава',          'Poltava Oblast',                'Полтавська',                -22, 178, -1.0,  7.9, 'DSTU-N B V.1.1-27:2010 (reference value, to be verified)'),
 (16, 'Rivne',           'Рівне',            'Rivne Oblast',                  'Рівненська',                -21, 183,  0.3,  7.3, 'DSTU-N B V.1.1-27:2010 (reference value, to be verified)'),
 (17, 'Sumy',            'Суми',             'Sumy Oblast',                   'Сумська',                   -24, 185, -1.6,  6.8, 'DSTU-N B V.1.1-27:2010 (reference value, to be verified)'),
 (18, 'Ternopil',        'Тернопіль',        'Ternopil Oblast',               'Тернопільська',             -21, 187,  0.3,  7.1, 'DSTU-N B V.1.1-27:2010 (reference value, to be verified)'),
 (19, 'Kharkiv',         'Харків',           'Kharkiv Oblast',                'Харківська',                -23, 179, -1.3,  7.8, 'DSTU-N B V.1.1-27:2010 (reference value, to be verified)'),
 (20, 'Kherson',         'Херсон',           'Kherson Oblast',                'Херсонська',                -19, 160,  1.3, 10.3, 'DSTU-N B V.1.1-27:2010 (reference value, to be verified)'),
 (21, 'Khmelnytskyi',    'Хмельницький',     'Khmelnytskyi Oblast',           'Хмельницька',               -21, 186, -0.3,  7.1, 'DSTU-N B V.1.1-27:2010 (reference value, to be verified)'),
 (22, 'Cherkasy',        'Черкаси',          'Cherkasy Oblast',               'Черкаська',                 -22, 177, -0.6,  8.0, 'DSTU-N B V.1.1-27:2010 (reference value, to be verified)'),
 (23, 'Chernivtsi',      'Чернівці',         'Chernivtsi Oblast',             'Чернівецька',               -20, 180,  0.6,  8.0, 'DSTU-N B V.1.1-27:2010 (reference value, to be verified)'),
 (24, 'Chernihiv',       'Чернігів',         'Chernihiv Oblast',              'Чернігівська',              -23, 185, -1.0,  7.3, 'DSTU-N B V.1.1-27:2010 (reference value, to be verified)'),
 (25, 'Simferopol',      'Сімферополь',      'Autonomous Republic of Crimea', 'Автономна Республіка Крим', -16, 151,  2.4, 10.4, 'DSTU-N B V.1.1-27:2010 (reference value, to be verified)');

-- ------------------------------------------------------------------ energy carriers (households, Ukraine)
INSERT INTO energy_carriers (code, name_en, name_uk, unit_en, unit_uk, energy_per_unit, price_per_unit, co2_per_kwh, source, updated_at) VALUES
 ('electricity',   'Electricity',                'Електроенергія',                 'kWh',   'кВт·год', 1.0,   4.32, 0.40,  'Household tariff since 2024-06-01; CO2: approximate grid emission factor', '2025-01-01'),
 ('natural_gas',   'Natural gas',                'Природний газ',                  'm³',    'м³',      9.3,   7.96, 0.202, 'Fixed household price (2024-2025); NCV ≈ 33.5 MJ/m³; CO2: IPCC 2006', '2025-01-01'),
 ('lpg',           'LPG (propane-butane)',       'Скраплений газ (пропан-бутан)',  'l',     'л',       6.8,  32.0,  0.227, 'Approximate retail price; CO2: IPCC 2006', '2025-01-01'),
 ('pellets',       'Wood pellets',               'Деревні пелети',                 'kg',    'кг',      4.8,   7.0,  0.0,   'Approximate price; ENplus A1 (NCV ≥ 4.6 kWh/kg); biogenic CO2 not counted', '2025-01-01'),
 ('firewood',      'Firewood (20 % moisture)',   'Дрова (вологість 20 %)',         'kg',    'кг',      4.0,   3.5,  0.0,   'Approximate price; biogenic CO2 not counted', '2025-01-01'),
 ('coal',          'Coal (anthracite)',          'Вугілля (антрацит)',             'kg',    'кг',      6.9,   9.0,  0.354, 'Approximate price; CO2: IPCC 2006', '2025-01-01'),
 ('district_heat', 'District heating',           'Централізоване теплопостачання', 'kWh',   'кВт·год', 1.0,   1.42, 0.25,  'Tariff ≈ 1654 UAH/Gcal (Kyiv); CO2: approximate', '2025-01-01');

-- ------------------------------------------------------------------ heat generators (seasonal efficiency, NCV basis)
INSERT INTO heating_technologies (code, name_en, name_uk, carrier_code, seasonal_efficiency, low_temperature_bonus, description_en, description_uk, source) VALUES
 ('gas_condensing',    'Gas condensing boiler',           'Газовий конденсаційний котел',             'natural_gas',   0.90, 0.08,
  'Efficiency rises as the flow temperature falls (condensation)', 'ККД зростає зі зниженням температури теплоносія (конденсація)', 'EN 15316-4-1; ErP ηs ≥ 86 % (GCV)'),
 ('gas_standard',      'Gas non-condensing boiler',       'Газовий конвекційний котел',               'natural_gas',   0.86, 0.0,
  'Conventional atmospheric / fan-assisted boiler', 'Традиційний атмосферний / турбований котел', 'EN 15316-4-1, typical values'),
 ('lpg_condensing',    'LPG condensing boiler',           'Конденсаційний котел на скрапленому газі', 'lpg',           0.90, 0.08,
  'Requires an LPG tank or cylinder installation', 'Потребує газгольдера або балонної установки', 'EN 15316-4-1'),
 ('electric_boiler',   'Electric boiler / panel heaters', 'Електрокотел / електроконвектори',         'electricity',   0.99, 0.0,
  'Direct electric heating', 'Пряме електричне опалення', 'EN 15316-4-5'),
 ('pellet_boiler',     'Pellet boiler',                   'Пелетний котел',                           'pellets',       0.85, 0.0,
  'Automatic fuel feed', 'Автоматична подача палива', 'EN 303-5, class 5; seasonal efficiency'),
 ('wood_gasification', 'Wood gasification boiler',        'Піролізний котел на дровах',               'firewood',      0.78, 0.0,
  'Recommended with a buffer tank', 'Рекомендовано з буферною ємністю', 'EN 303-5; seasonal efficiency'),
 ('coal_boiler',       'Solid fuel boiler (coal)',        'Твердопаливний котел (вугілля)',           'coal',          0.72, 0.0,
  'Manual fuel loading', 'Ручне завантаження палива', 'EN 303-5; seasonal efficiency'),
 ('district_heating',  'District heating (substation)',   'Централізоване опалення (ІТП)',            'district_heat', 0.96, 0.0,
  'Losses of the building heat substation', 'Втрати в індивідуальному тепловому пункті', 'EN 15316-4-5');

-- ------------------------------------------------------------------ minimum thermal resistance R_q,min (residential)
INSERT INTO envelope_requirements (climate_zone, element, min_resistance, source) VALUES
 (1, 1, 3.30, 'DBN V.2.6-31:2016, table 3'), (2, 1, 2.80, 'DBN V.2.6-31:2016, table 3'),
 (1, 2, 6.00, 'DBN V.2.6-31:2016, table 3'), (2, 2, 5.35, 'DBN V.2.6-31:2016, table 3'),
 (1, 3, 4.95, 'DBN V.2.6-31:2016, table 3'), (2, 3, 4.50, 'DBN V.2.6-31:2016, table 3'),
 (1, 4, 3.75, 'DBN V.2.6-31:2016, table 3'), (2, 4, 3.30, 'DBN V.2.6-31:2016, table 3'),
 (1, 5, 6.00, 'DBN V.2.6-31:2016, table 3'), (2, 5, 5.35, 'DBN V.2.6-31:2016, table 3'),
 (1, 6, 0.75, 'DBN V.2.6-31:2016, table 3'), (2, 6, 0.60, 'DBN V.2.6-31:2016, table 3');
